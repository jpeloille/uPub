#!/usr/bin/env bash
#
# Recette du poste N19 — le corps et la police : une liste, et une frappe (fiche 23, b6).
#
# Publisher propose le corps dans une liste de tailles où l'on tape aussi, et la police dans une
# liste où l'on tape son nom (p. 10, 48). Avant le poste, le corps de la barre contextuelle était
# un champ sans liste, et la liste des polices ne se tapait pas. Le corps garde son champ, où l'on
# tape une valeur décimale (N14), et le chevron qui le suit ouvre la liste standard du noyau
# (CorpsStandard, N10) ; la police se tape, la liste propose les familles qui commencent par la
# frappe, et Entrée pose la première. Pour le texte comme pour le tableau. Deux temps.
#
# 1. LES SOURCES, lues par un script :
#
#      a. la liste des polices de la barre, texte et tableau, est une ListeDesPolices : une
#         ComboBox d'Avalonia éditable, qui en garde l'apparence (StyleKeyOverride), et dont la
#         recherche à chaque lettre — l'entrée de même texte prise pendant la frappe — ne joue
#         pas ; la garde de Q20 tient, JamaisNul sur la liaison, et les deux listes se lient aux
#         polices du modèle de vue (FontFamilies), celles du document absentes de la machine
#         comprises ;
#      b. le refus passe par la barre d'état (Status), le canal de N14 ;
#      c. le corps est toujours un ChampNumerique, suivi d'un chevron « Tailles du corps » dont
#         les entrées sont les tailles du noyau, et dont la commande est au partiel du poste
#         (règle 10) ; Entrée dans la liste des polices rend la planche, comme après un champ ;
#      d. le README le dit ; ListeDesPolices.cs est une vue de la phase (recette I1).
#
# 2. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre
#    (« CorpsEtPoliceTests », trait « poste=N19 ») — les clauses de la recette du plan :
#
#      · taper « Geo » propose Georgia, et Entrée la pose en un pas — la machine du banc n'a pas
#        Georgia : un autre bloc du document l'emploie, et elle est proposée « (absente) » ;
#        « dejavu se » propose DejaVu Serif, sans regarder la casse ; une entrée du jeu se tape
#        par son libellé, que la zone montre ;
#      · la frappe ne pose rien avant Entrée — le sabotage du juge — : lettre à lettre, le bloc
#        garde sa police, et l'entrée exacte passe devant ; ↑ et ↓ promènent la marque ;
#      · une police que la machine n'a pas est refusée, et la barre d'état le dit ; Échap
#        renonce et rend la planche ; quitter la liste au pointeur renonce ; Tab pose et passe
#        au corps ; la police choisie au pointeur fait un pas ; le tableau a la même liste ;
#      · une police absente du document reste proposée « (absente) » (Q20), et se retape ;
#      · la liste des tailles pose 14 pt en un pas, au texte et au tableau, et coche la taille
#        du bloc ; la même taille ne fait pas de pas ; « 10,5 » tapé donne 10,5 pt, ↑ fait un pas
#        de 0,5 ; un corps tapé part avant la taille de la liste, chacun son pas ; ouvrir et
#        refermer la liste ne réécrit pas un corps que l'affichage arrondit (la mémoire de Q14).
#
#    Puis les voisins : la police absente (Q20), la barre (I3) et ses .Single() (règle 1), les
#    champs de la barre (N14).
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

# ---------------------------------------------------------------------- 1. les sources

python3 - "$racine" <<'PY'
import pathlib, re, sys

racine = pathlib.Path(sys.argv[1])
VM = racine / "MyPublisher/ViewModels"
VUES = racine / "MyPublisher/Views"

echecs = []
def exige(condition, message):
    if not condition:
        echecs.append(message)

def lire(chemin):
    return chemin.read_text(encoding="utf-8") if chemin.exists() else ""

def corps(source, signature):
    """Le corps d'une méthode, de son accolade ouvrante à la fermante de même retrait."""
    trouve = re.search(re.escape(signature) + r"\s*\n(\s*)\{\n(.*?)\n\1\}", source, re.S)
    return trouve.group(2) if trouve else None

barre_axaml = lire(VUES / "BarreContextuelle.axaml")
barre = lire(VUES / "BarreContextuelle.axaml.cs")
liste = lire(VUES / "ListeDesPolices.cs")
partiel = lire(VM / "MainWindowViewModel.CorpsEtPolice.cs")
noyau = lire(racine / "MyPublisher.Core/Models/CorpsStandard.cs")

# ------------------------------------------------------ a. la liste des polices se tape

exige("public class ListeDesPolices : ComboBox" in liste, "ListeDesPolices ne dérive pas de la ComboBox d'Avalonia.")
exige("protected override Type StyleKeyOverride => typeof(ComboBox);" in liste,
      "ListeDesPolices ne garde pas l'apparence d'une ComboBox (StyleKeyOverride).")
exige("IsEditable = true;" in liste, "la liste des polices ne se tape pas (IsEditable).")
propriete = corps(liste, "protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)") or ""
exige(re.search(r"if \(change\.Property == TextProperty\)\s*\{(?:\s*//[^\n]*)*\s*if \(!_ecrit\) Proposer\(change\.GetNewValue<string\?>\(\) \?\? \"\"\);\s*return;\s*\}", propriete),
      "la frappe passe encore par la recherche de la ComboBox, qui prend l'entrée de même texte à chaque lettre.")
exige("base.OnPropertyChanged(change);" in propriete, "ListeDesPolices ne laisse plus la ComboBox suivre ses autres propriétés.")
polices = [m for m in re.finditer(r"<views:ListeDesPolices\s(.*?)>", barre_axaml, re.S)]
exige(len(polices) == 2, f"la barre a {len(polices)} listes des polices, et non deux (texte et tableau).")
for m in polices:
    attributs = m.group(1)
    exige('SelectedItem="{Binding FontFamily, Converter={x:Static conv:JamaisNul.Instance}}"' in attributs,
          "une liste des polices ne garde plus la liaison de Q20 (JamaisNul).")
    exige("DataContext).FontFamilies}" in attributs, "une liste des polices ne se lie plus aux polices du modèle de vue.")
exige("<ComboBox " not in barre_axaml, "la barre garde une ComboBox qui ne se tape pas.")
exige(barre_axaml.count('<views:ListeDesPolices x:Name="PoliceDuBloc"') == 1, "la police du bloc de texte n'a plus son nom (PoliceDuBloc).")
print("    a. la police se tape, texte et tableau, sans la recherche à chaque lettre ; la garde de Q20 tient")

# ---------------------------------------------------------- b. le refus est dit

valider = corps(liste, "public bool Valider()") or ""
exige("vm.Status = $\"« {saisie} » n'est pas une police de cette machine : la police reste {avant}.\";" in valider,
      "le refus d'une police que la machine n'a pas n'est pas dit dans la barre d'état.")
print("    b. une police que la machine n'a pas est refusée, et la barre d'état le dit")

# ---------------------------------------------------- c. le corps et sa liste

exige(barre_axaml.count("<views:ChampNumerique ") == 4, "la barre n'a plus ses quatre champs numériques (N14).")
exige(barre_axaml.count('ToolTip.Tip="Tailles du corps"') == 2, "le corps du texte et celui du tableau n'ont pas chacun leur liste.")
exige(barre_axaml.count('Opening="SurTaillesQuiSOuvrent" Closed="SurTaillesFermees"') == 2, "la liste des tailles ne suit plus son ouverture.")
attache = corps(barre, "private void SurTaillesAttachees(object? sender, VisualTreeAttachmentEventArgs e)") or ""
exige("foreach (var taille in CorpsStandard.Tailles)" in attache, "la liste des tailles n'est pas celle du noyau (CorpsStandard, N10).")
exige("Command = vm.PoserLeCorpsCommand," in attache, "une taille de la liste ne joue pas la commande du poste.")
exige(re.search(r"\[RelayCommand\(CanExecute = nameof\(PorteDuTexte\)\)\]\s*private void PoserLeCorps\(double taille\) => ChangerLeCorps\(_ => taille\);", partiel),
      "la commande de la liste n'est pas au partiel du poste, ou ne passe pas par ChangerLeCorps (un pas, N10).")
exige("using Avalonia" not in noyau, "la liste standard du noyau dépend d'Avalonia.")
exige(re.search(r"if \(e\.Key == Key\.Enter && dansLaListeDesPolices\)\s*\{\s*focus\.ClearFocus\(\);", barre),
      "Entrée, dans la liste des polices, ne rend pas la planche.")
exige("if (e.Key == Key.Enter && champ.FindAncestorOfType<ChampNumerique>(includeSelf: true) is null) return;" in barre,
      "la garde d'Entrée de N14 a changé.")
print("    c. le corps garde son champ et reçoit la liste du noyau, au partiel du poste ; Entrée rend la planche")

# ------------------------------------------------------------- d. le README, I1

readme = lire(racine / "README.md")
exige("La police se tape :" in readme, "le README ne dit pas que la police se tape.")
exige("que le chevron qui le suit ouvre" in readme, "le README ne dit pas la liste des tailles.")
exige("| Dans la liste des polices de la barre : `Entrée` ou `Tab`, `↑` / `↓`, `Échap` |" in readme,
      "le tableau des raccourcis ne dit pas les touches de la liste des polices.")
exige("    MyPublisher/Views/ListeDesPolices.cs\n" in lire(racine / "Scripts/recettes/I1-apparence.sh"),
      "ListeDesPolices.cs n'est pas dans les vues de la phase (recette I1).")
print("    d. le README dit la police tapée et la liste des tailles ; ListeDesPolices.cs est une vue de la phase")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : la police tapée sans recherche à chaque lettre, Q20 gardé, le refus dit, la liste des tailles du noyau, le README"

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

jouer "poste=N19" 16 "2. le corps et la police, sur la vraie fenêtre : « Geo » propose Georgia, la frappe ne pose rien, le refus dit, Échap, la liste des tailles, « 10,5 »"

# Les voisins : la police absente (Q20), la barre et ses .Single() (I3, règle 1), les champs (N14).
voisins="poste=Q20"
voisins+="|FullyQualifiedName~BarreContextuelleTests.Chaque_propriete_n_a_qu_un_controle_visible"
voisins+="|FullyQualifiedName~BarreContextuelleTests.Chaque_controle_de_la_barre_fait_un_seul_pas"
voisins+="|FullyQualifiedName~BarreContextuelleTests.T_dans_le_corps_s_ecrit_et_T_apres_Echap_arme_l_outil_texte"
voisins+="|FullyQualifiedName~ChampNumeriqueTests.Echap_renonce_et_rend_la_planche_dans_la_barre_comme_au_dock"
jouer "$voisins" 10 "   les voisins : la police absente (Q20), un contrôle par propriété et un pas par contrôle (I3), Échap (I3, N14)"

echo "N19 : le corps se tape ou se choisit dans la liste standard ; la police se tape, la liste propose, et Entrée la pose."
