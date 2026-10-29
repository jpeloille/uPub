#!/usr/bin/env bash
#
# Recette du poste N13 — les menus de Publisher : des lettres d'accès uniques et sans accent,
# « _Edition » joignable, Disposition rangé comme Publisher 2007 avec son « Regrouper », Tab et
# Maj+Tab qui passent d'un bloc à l'autre sur la planche. Deux temps.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. à chaque niveau de chaque menu de la fenêtre — la barre, chaque menu, chaque
#         sous-menu —, chaque entrée a une lettre d'accès, une seule fois au niveau, et aucune
#         n'est accentuée : Avalonia compare la lettre au nom de la touche (Key.ToString()), et
#         une lettre en double fait tourner le focus d'une entrée à l'autre sans rien
#         déclencher ; « _Edition » a remplacé « _Édition » ;
#      b. Disposition est rangé comme celui de Publisher 2007 (le livre, p. 16) : « Ordre ▸ »
#         et ses quatre ordres, « Aligner ou répartir ▸ » et ses six alignements, ses deux
#         répartitions et leur repère, « Regrouper » sur sa commande — et, depuis N18, « Repères
#         de mise en page… » en tête et « Faire pivoter ou retourner ▸ » après l'alignement,
#         comme Layout Guides… et Rotate or Flip ▸ chez Publisher ; les cibles des autres
#         restent telles quelles — « Ajuster aux _marges » (le juge), « Tr_ait » (A10) ;
#      c. « Regrouper » : le groupe retenu d'une ligne dans UngroupSelected, la commande dans
#         le partiel du poste (règle 10), sa disponibilité suivie avec la sélection ;
#      d. Tab et Maj+Tab : pris par OnKeyDown après la garde de la saisie, sur la planche
#         seule, avant la navigation du focus ; au tableau des raccourcis du README ;
#      e. la barre de menus ouverte garde ses touches : OnKeyDown la laisse passer avant les
#         lettres d'outil et Suppr ;
#      f. la lettre d'accès se cherche au seul niveau ouvert, comme Windows (repris le 29 octobre
#         2026, sur le choix du propriétaire) : Avalonia 11.3.9 la cherchait sur la barre et sur
#         chaque niveau ouvert à la fois, et P, Disposition puis Ordre ouverts, désignait « _Page »,
#         « Re_pères de règle » et « Premier _plan » sans rien jouer. MainWindow.Menus.cs descend
#         au niveau ouvert le plus profond, compare la lettre au caractère — au chiffre pour la
#         rangée des chiffres —, et OnKeyDown l'appelle avant la garde de la saisie ;
#      g. les gestes des menus s'écrivent en français, « Ctrl+Maj+G », comme les bulles (repris
#         le 29 octobre 2026) : AppBuilder.With(new KeyGestureFormatInfo(…)) ne tient pas sous
#         Avalonia 11.3.9 — la plate-forme lie la sienne par-dessus —, et GestesEnFrancais écrit
#         le texte en valeur locale sur PART_InputGestureText à l'application du gabarit de
#         chaque MenuItem, branché une fois par App.Initialize, que l'application, l'hôte des
#         tests et le banc montent tous ; le thème n'est pas recopié.
#
# 2. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre, au clavier et
#    au pointeur synthétisés (« MenusDePublisherTests », trait « poste=N13 ») :
#
#      · chaque niveau des menus, sous-menus et documents récents compris, a des lettres
#        d'accès uniques et sans accent ; la barre porte « _Edition » ;
#      · Alt relâché puis E ouvre Edition, Alt puis T ouvre Texte, sans armer d'outil ; Alt+E
#        tenu aussi ;
#      · Alt, D, O, P joue Premier plan en un pas — la clause du plan —, Alt, D, O, L
#        Arrière-plan, et Suppr, menus ouverts, ne supprime rien ; Edition ouvert, A annule et
#        n'ouvre pas Affichage ; deux entrées d'un niveau sur une lettre se passent le focus sans
#        rien jouer, une lettre absente du niveau ne fait rien ; Alt, F puis « 2 » (« é » sur
#        l'AZERTY) ouvre le deuxième document récent ;
#      · les menus disent « Ctrl+Maj+G », « Ctrl+Maj+F », « Suppr », « Ctrl++ », « Ctrl+− »,
#        et le texte suit un geste qui change ;
#      · Disposition rangé comme Publisher, chaque entrée sur sa commande, la case du repère
#        dans les deux sens ;
#      · Regrouper refait le groupe en un pas, à son nom, même un bloc déplacé, et ne s'offre
#        plus quand un des blocs a quitté la page ;
#      · Tab et Maj+Tab sautent un bloc masqué et repartent de l'autre bout, sans pas
#        d'annulation, la planche gardant le focus ; Tab dans le dock va au champ suivant.
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

def lire(chemin):
    return (racine / chemin).read_text(encoding="utf-8")

axaml = lire("MyPublisher/Views/MainWindow.axaml")
fenetre = ET.fromstring(axaml)
code = lire("MyPublisher/Views/MainWindow.axaml.cs")
vm = lire("MyPublisher/ViewModels/MainWindowViewModel.cs")
partiel_chemin = racine / "MyPublisher/ViewModels/MainWindowViewModel.Menus.cs"
partiel = partiel_chemin.read_text(encoding="utf-8") if partiel_chemin.exists() else ""
readme = lire("README.md")

def lettre(entete):
    """La lettre d'accès telle qu'Avalonia la lit : après le premier « _ » seul ; « __ » est un trait."""
    i = 0
    while i < len(entete) - 1:
        if entete[i] == "_":
            if entete[i + 1] == "_":
                i += 2
                continue
            return entete[i + 1]
        i += 1
    return None

# ------------------------------------------------- a. une lettre par entrée, unique, sans accent
barres = [e for e in fenetre.iter(A + "Menu") if e.get(X + "Name") == "Menus"]
exige(len(barres) == 1, "la barre de menus « Menus » n'est pas dans la fenêtre, une fois.")
niveaux = 0
for parent in [*barres, *fenetre.iter(A + "MenuItem")]:
    entrees = [e for e in parent if e.tag == A + "MenuItem"]
    if not entrees:
        continue
    niveaux += 1
    nom = parent.get("Header") or "la barre"
    vues = {}
    for e in entrees:
        entete = e.get("Header") or ""
        l = lettre(entete)
        if l is None:
            exige(False, f"« {entete} », dans {nom}, n'a pas de lettre d'accès.")
            continue
        exige(l.isascii() and l.isalnum(), f"« {entete} », dans {nom} : la lettre d'accès « {l} » est accentuée.")
        vues.setdefault(l.upper(), []).append(entete)
    for l, entetes in vues.items():
        exige(len(entetes) == 1, f"{nom} : la lettre d'accès {l} est en double ({', '.join(entetes)}).")
exige(niveaux >= 11, f"{niveaux} niveaux de menu lus, onze attendus au moins.")
exige(re.search(r'<MenuItem Header="_Edition">', axaml) is not None, "le menu « _Edition » manque.")
exige("_Édition" not in re.sub(r"<!--.*?-->", "", axaml, flags=re.S), "« _Édition », injoignable, est encore à la barre.")
print(f"    a. {niveaux} niveaux de menu : une lettre d'accès par entrée, unique au niveau, sans accent ; « _Edition »")

# ---------------------------------------------------------- b. Disposition rangé comme Publisher
def entrees_de(entete):
    menus = [e for e in fenetre.iter(A + "MenuItem") if e.get("Header") == entete]
    exige(len(menus) == 1, f"le menu « {entete} » n'est pas dans la fenêtre, une fois.")
    return [(e.get("Header"), e.get("Command") or e.get("IsChecked")) for e in menus[0] if e.tag == A + "MenuItem"] if menus else []

exige([h for h, _ in entrees_de("_Disposition")] ==
      ["Repères de m_ise en page…", "Re_pères de règle", "_Grouper", "_Dissocier", "_Regrouper", "_Ordre", "_Aligner ou répartir",
       "_Faire pivoter ou retourner", "Ajuster aux _marges"],
      f"Disposition n'est pas rangé comme Publisher : {[h for h, _ in entrees_de('_Disposition')]}.")
exige(entrees_de("_Ordre") == [("Premier _plan", "{Binding BringToFrontCommand}"), ("Arrière-p_lan", "{Binding SendToBackCommand}"),
                               ("_Avancer", "{Binding BringForwardCommand}"), ("_Reculer", "{Binding SendBackwardCommand}")],
      "Disposition ▸ Ordre ne porte pas ses quatre ordres sur leurs commandes.")
alignements = entrees_de("_Aligner ou répartir")
exige(len(alignements) == 9 and alignements[-1] == ("Par rapport aux _marges", "{Binding AlignToMargins, Mode=TwoWay}"),
      "Disposition ▸ Aligner ou répartir ne porte pas ses six alignements, ses deux répartitions et leur repère.")
exige(dict(entrees_de("_Disposition")).get("_Regrouper") == "{Binding RegrouperCommand}", "« Regrouper » n'est pas sur RegrouperCommand.")
exige(axaml.count('<MenuItem Header="Ajuster aux _marges" Command="{Binding FitToMarginsCommand}"/>') == 1,
      "« Ajuster aux _marges », cible du juge, a changé.")
exige(re.search(r'<MenuItem Header="Tr_ait" Command="\{Binding AddLineCommand\}"/>', axaml) is not None, "« Tr_ait » (A10) a changé.")
print("    b. Disposition : Ordre ▸, Aligner ou répartir ▸, Regrouper ; Ajuster aux marges et Trait intacts")

# ------------------------------------------------------------------------------ c. Regrouper
corps = re.search(r"private void UngroupSelected\(\)\s*\{(.*?)\n    \}", vm, re.S)
exige(corps is not None and corps.group(1).count("RetenirLeGroupeDissous(group, children);") == 1,
      "UngroupSelected ne retient pas le groupe qu'il dissout.")
exige("RegrouperCommand.NotifyCanExecuteChanged();" in vm, "la disponibilité de Regrouper ne suit pas la sélection.")
exige("public partial class MainWindowViewModel" in partiel, "le partiel du poste, MainWindowViewModel.Menus.cs, manque (règle 10).")
for membre in ("private void Regrouper()", "RetenirLeGroupeDissous", "public bool PeutRegrouper", "public bool SelectionnerLeBlocVoisin"):
    exige(membre in partiel, f"« {membre} » n'est pas dans le partiel du poste.")
print("    c. Regrouper : le groupe retenu à la dissociation, la commande au partiel du poste")

# ------------------------------------------------------------------------------ d. Tab
cle = re.search(r"protected override void OnKeyDown\(KeyEventArgs e\)\s*\n(\s*)\{\n(.*?)\n\1\}", code, re.S)
exige(cle is not None, "OnKeyDown est introuvable.")
if cle:
    texte = cle.group(2)
    garde = texte.find("if (PageView.IsEditingText || ConsumesTyping(")
    tab = texte.find("case Key.Tab when")
    exige(garde >= 0 and tab > garde, "Tab n'est pas pris par OnKeyDown, après la garde de la saisie.")
    exige(re.search(r"case Key\.Tab when [^\n]*LaPlancheALeFocus\(\):\s*\n\s*vm\.SelectionnerLeBlocVoisin\(enArriere: fine\);", texte)
          is not None, "Tab ne passe pas au bloc voisin sur la planche seule.")
    menus = texte.find("if (Menus.IsOpen)")
    outils = texte.find("case Key.E: vm.ActiveTool")
    exige(0 <= menus < outils, "la barre de menus ouverte ne garde pas ses touches avant les lettres d'outil.")
exige(re.search(r"private bool LaPlancheALeFocus\(\) =>", code) is not None, "LaPlancheALeFocus manque.")
tableau = re.search(r"^## Raccourcis$(.*?)^## ", readme, re.S | re.M)
exige(tableau is not None and "| `Tab` / `Maj+Tab` |" in tableau.group(1), "README : Tab et Maj+Tab ne sont pas au tableau des raccourcis.")
print("    d. Tab et Maj+Tab sur la planche, après la garde de la saisie, au README ; e. la barre de menus ouverte garde ses touches")

# ------------------------------------------------------------ f. la lettre au seul niveau ouvert
menus_cs = racine / "MyPublisher/Views/MainWindow.Menus.cs"
menus_texte = menus_cs.read_text(encoding="utf-8") if menus_cs.exists() else ""
exige(re.search(r"private bool JouerLaLettreDuMenu\(KeyEventArgs e\)", menus_texte) is not None,
      "MainWindow.Menus.cs ne porte pas JouerLaLettreDuMenu.")
exige(re.search(r"var trouvees = EntreesDuNiveauOuvert\(\)", menus_texte) is not None,
      "la lettre d'accès ne se cherche pas au seul niveau ouvert.")
exige(re.search(r"while \(niveau\.FirstOrDefault\(entree => entree\.IsSubMenuOpen\) is \{ \} ouverte\)", menus_texte) is not None,
      "EntreesDuNiveauOuvert ne descend pas au niveau ouvert le plus profond.")
exige(re.search(r"e\.Key is >= Key\.D0 and <= Key\.D9", menus_texte) is not None and "e.KeySymbol" in menus_texte,
      "la lettre ne se compare ni au chiffre de la rangée ni au caractère produit.")
if cle:
    appel = texte.find("if (JouerLaLettreDuMenu(e))")
    exige(0 <= appel < garde, "OnKeyDown n'appelle pas JouerLaLettreDuMenu avant la garde de la saisie.")
    exige(texte.find("vm.JouerLeRaccourciParCaractere(e.KeySymbol)") < appel, "la table de N10 n'est plus lue en premier.")
print("    f. la lettre d'accès cherchée au seul niveau ouvert, sur le caractère, avant la garde de la saisie")

# -------------------------------------------------------------- g. les gestes en français
gestes_cs = racine / "MyPublisher/Views/GestesEnFrancais.cs"
gestes = gestes_cs.read_text(encoding="utf-8") if gestes_cs.exists() else ""
exige('PartieDuGeste = "PART_InputGestureText"' in gestes and "TemplateAppliedEvent.AddClassHandler<MenuItem>" in gestes
      and "MenuItem.InputGestureProperty.Changed.AddClassHandler<MenuItem>" in gestes,
      "GestesEnFrancais n'écrit pas le geste à l'application du gabarit, ni ne le suit quand il change.")
for nom, mot in (("Shift", '"Maj"'), ("Delete", '"Suppr"')):
    exige(mot in gestes, f"GestesEnFrancais n'écrit pas {nom} en {mot}.")
app = lire("MyPublisher/App.axaml.cs")
initialiser = re.search(r"public override void Initialize\(\)\s*\{(.*?)\n    \}", app, re.S)
exige(initialiser is not None and "GestesEnFrancais.Brancher();" in initialiser.group(1),
      "App.Initialize ne branche pas les gestes en français.")
exige(not re.search(r"KeyGestureFormatInfo\(", re.sub(r"//.*|<!--.*?-->|///.*", "", lire("MyPublisher/Program.cs"))),
      "Program.cs monte un KeyGestureFormatInfo, que la plate-forme écraserait.")
exige(not re.search(r"<ControlTheme[^>]*TargetType=\"MenuItem\"", axaml), "le thème du MenuItem est recopié.")
print("    g. les gestes des menus en français, par GestesEnFrancais, branché dans App.Initialize")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : lettres d'accès, Disposition, Regrouper, Tab, la barre ouverte, la lettre au niveau ouvert, les gestes en français"

# ------------------------------------------------------------------------ 2. les tests

attendus=11
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=N13" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les tests d'interface sous « poste=N13 » ne passent pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=N13 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  2. la fenêtre : lettres d'accès, Alt puis E et T, Alt D O P et L, Edition puis A, la lettre partagée, les récents, les gestes en français, Disposition, Regrouper, Tab sur la planche et dans le dock ($joues tests)"

echo "N13 : les menus ont des lettres d'accès uniques et joignables, Disposition est rangé comme Publisher, Tab passe d'un bloc à l'autre."
