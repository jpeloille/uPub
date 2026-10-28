#!/usr/bin/env bash
#
# Recette du poste N14 — les champs (fiche 23, f2, f5, f7, f8, f10, f11).
#
# Les champs numériques du dock et de la barre contextuelle se tiennent comme ceux d'Office.
# La valeur ne part plus à chaque frappe lisible, mais à la validation — Entrée, Tab, la sortie
# du champ, ↑ et ↓, la molette — ; une autre unité se tape et se convertit, l'affichage restant
# en millimètres (décision du 24 octobre 2026) ; un refus rend la valeur d'avant et le dit dans
# la barre d'état, bornes comprises ; Échap renonce et rend la main à la planche (règle 13), et
# Entrée, dans la barre, valide et la rend aussi ; le catalogue et le dialogue des coordonnées
# se ferment par Échap ; Tab ne s'arrête plus sur les titres de section du dock. Deux temps.
#
# 1. LES SOURCES, lues par un script :
#
#      a. aucun NumericUpDown d'Avalonia ne reste dans la fenêtre ni dans la barre : chaque
#         champ numérique y est un ChampNumerique, qui en garde l'apparence (StyleKeyOverride)
#         et dont la frappe ne pousse rien (OnTextChanged vide) ;
#      b. le convertisseur des champs (Unite) lit mm, cm, pt, pi et le pouce, en fractions
#         exactes de millimètre, et écrit toujours dans l'unité du champ ; la cible du juge
#         « texte = separateur == "," … » est toujours là, une fois ;
#      c. ce qui attend dans un champ part avant que sa transaction ne se referme
#         (ClosePropertyEdit, le piège du plan) et avant que le modèle de vue ne referme les
#         siennes (EndPendingEdit), ne change la sélection (SetSelection), ne lise le document
#         pour l'enregistrer ou l'exporter ; ce que le poste ajoute au modèle de vue est dans
#         son partiel (règle 10) ;
#      d. le refus passe par la barre d'état du modèle de vue (Status), le canal du zoom tapé
#         de N4 — aucun autre ;
#      e. les deux dialogues ont leur bouton d'annulation (IsCancel) ; les titres de section du
#         dock ne sont pas des arrêts de Tab ;
#      f. le README le dit ; ChampNumerique.cs est une vue de la phase (recette I1).
#
# 2. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre, au clavier
#    (« ChampNumeriqueTests », trait « poste=N14 ») — les clauses de la recette du plan :
#
#      · « 2 cm » dans X donne 20,0 mm ; « 12 pt » dans une marge, 4,23 mm ; « 1" », 25,4 mm ;
#        « 2,5 cm », « 6 pi », « 2 po », « 1in », et « 4 mm » dans le corps, en points ;
#      · en tapant « 210 », la largeur de la page n'est écrite qu'une fois, à Entrée, en un pas ;
#        Tab valide, ↑ et ↓ font un pas à partir de ce qui est tapé, la molette aussi ;
#      · « trente » laisse la valeur et le dit, et ↑ fait encore un pas ; 5 000 mm de large est
#        refusé, bornes dites, « 2 m » aussi, à la sortie du champ, sans pas d'annulation ;
#      · Échap dans le corps rend la valeur d'avant et la planche, « T » arme l'outil texte ; de
#        même au dock ; Entrée dans la barre valide et rend la planche, au dock garde le champ ;
#      · ce qui attend dans Y part avant Annuler, et avant l'enregistrement ; fermer le
#        document pendant qu'un champ attend pose la question (le piège du plan) ; ce qui
#        attend dans X, L ou le corps reste au bloc qu'on quitte quand la sélection change ;
#      · retaper ce qui est affiché, ou traverser les champs, ne réécrit rien (la mémoire de
#        Q14) ;
#      · Tab passe de X à Y, L, H, l'angle, sans s'arrêter sur un titre de section ; Maj+Tab
#        revient ;
#      · Échap ferme le catalogue et le dialogue des coordonnées.
#
#    Puis les voisins réécrits (règle 14) ou gardés : les trois tests de la barre où Entrée
#    gardait le champ, ou Échap validait (I3) ; « 40 » tapé dans Y reste au bloc au
#    changement d'onglet (DispositionTests, le piège du plan).
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

def corps(source, signature):
    """Le corps d'une méthode, de son accolade ouvrante à la fermante de même retrait."""
    trouve = re.search(re.escape(signature) + r"\s*\n(\s*)\{\n(.*?)\n\1\}", source, re.S)
    return trouve.group(2) if trouve else None

def lire(chemin):
    return chemin.read_text(encoding="utf-8") if chemin.exists() else ""

fenetre_axaml = lire(VUES / "MainWindow.axaml")
barre_axaml = lire(VUES / "BarreContextuelle.axaml")
champ = lire(VUES / "ChampNumerique.cs")
fenetre = lire(VUES / "MainWindow.axaml.cs")
barre = lire(VUES / "BarreContextuelle.axaml.cs")
modele = lire(VM / "MainWindowViewModel.cs")
partiel = lire(VM / "MainWindowViewModel.Champs.cs")
unite = lire(racine / "MyPublisher/Converters/DispositionConverters.cs")

# ----------------------------------------------- a. un ChampNumerique pour chaque champ

for nom, source in (("MainWindow.axaml", fenetre_axaml), ("BarreContextuelle.axaml", barre_axaml)):
    exige("<NumericUpDown" not in source, f"{nom} garde un NumericUpDown d'Avalonia, qui pousse sa valeur à la frappe.")
    exige("<views:ChampNumerique " in source, f"{nom} n'a aucun ChampNumerique.")
exige(fenetre_axaml.count("<views:ChampNumerique ") >= 40, "la fenêtre a moins de quarante champs numériques : en manque-t-il ?")
exige(barre_axaml.count("<views:ChampNumerique ") == 4, "la barre n'a pas ses quatre champs numériques (corps et interligne, texte et tableau).")
exige("public class ChampNumerique : NumericUpDown" in champ, "ChampNumerique ne dérive pas de NumericUpDown.")
exige("protected override Type StyleKeyOverride => typeof(NumericUpDown);" in champ,
      "ChampNumerique ne garde pas l'apparence d'un NumericUpDown (StyleKeyOverride).")
exige(re.search(r"protected override void OnTextChanged\(string\? oldValue, string\? newValue\)\s*\{\s*\}", champ),
      "la frappe pousse encore la valeur : OnTextChanged n'est pas vide.")
for geste in ("protected override void OnKeyDown(KeyEventArgs e)", "protected override void OnLostFocus(RoutedEventArgs e)",
              "protected override void OnSpin(SpinEventArgs e)"):
    exige(geste in champ, f"ChampNumerique ne tient pas « {geste} ».")
touche = corps(champ, "protected override void OnKeyDown(KeyEventArgs e)") or ""
exige(re.search(r"case Key\.Escape when e\.KeyModifiers == KeyModifiers\.None:\s*Abandonner\(\);\s*TopLevel\.GetTopLevel\(this\)\?\.FocusManager\?\.ClearFocus\(\);", touche),
      "Échap ne renonce pas avant de rendre la planche (règle 13).")
print("    a. chaque champ numérique de la fenêtre et de la barre est un ChampNumerique, que la frappe ne fait pas partir")

# ----------------------------------------------------------------- b. les unités lues

for suffixe, numerateur, denominateur in (("mm", "1m", "1m"), ("cm", "10m", "1m"), ("pt", "25.4m", "72m"),
                                           ("pi", "25.4m", "6m"), ("po", "25.4m", "1m"), ("in", "25.4m", "1m"),
                                           ('\\"', "25.4m", "1m")):
    exige(f'("{suffixe}", {numerateur}, {denominateur}),' in unite, f"l'unité « {suffixe} » n'est pas lue, ou pas à sa fraction de millimètre.")
exige(unite.count("        texte = separateur == \",\" ? texte.Replace('.', ',') : texte.Replace(',', '.');") == 1,
      "la cible du juge « texte = separateur == \",\" … » n'est plus là une fois.")
exige('decimal nombre => nombre.ToString(_format, culture) + (_espace ? " " : "") + _unite,' in unite,
      "l'affichage n'est plus dans l'unité du champ (décision du 24 octobre 2026).")
print("    b. mm, cm, pt, pi et le pouce lus en fractions exactes ; l'affichage dans l'unité du champ ; la cible du juge en place")

# ---------------------------------------- c. ce qui attend part avant qu'on referme

fermer = corps(fenetre, "private void ClosePropertyEdit()") or ""
exige(re.search(r"^\s*(//.*\n\s*)*ValiderLeChampNumerique\(\);\s*if \(!_propertyEditOpen\) return;", fermer),
      "ClosePropertyEdit ne valide pas le champ qui attend avant de refermer sa transaction (le piège du plan).")
exige("_champNumerique = (e.Source as Visual)?.FindAncestorOfType<ChampNumerique>(includeSelf: true);" in fenetre,
      "la fenêtre ne retient pas le champ numérique qui prend le focus.")
exige(fenetre.count("_vm.SuivreLesChamps(ValiderLeChampNumerique);") == 1 and fenetre.count("_vm.SuivreLesChamps(null);") == 1,
      "la fenêtre ne prête pas — ou ne reprend pas — la validation du champ au modèle de vue.")
fin = corps(modele, "private void EndPendingEdit()") or ""
exige(fin.strip().startswith("ValiderLeChampEnAttente();"),
      "EndPendingEdit ne valide pas le champ qui attend : Annuler défairait autre chose que la frappe.")
for signature in ("public void SaveTo(string path)", "public void ExportPdf(string path)",
                  "public void SetSelection(IReadOnlyList<Frame> frames)"):
    exige("ValiderLeChampEnAttente();" in (corps(modele, signature) or ""),
          f"« {signature} » lit le document sans valider le champ qui attend.")
exige("public partial class MainWindowViewModel" in partiel and "public void SuivreLesChamps(Action? valider)" in partiel,
      "ce que N14 ajoute au modèle de vue n'est pas dans son partiel, MainWindowViewModel.Champs.cs (règle 10).")
exige("SuivreLesChamps" not in modele and "_validerLeChamp" not in modele,
      "le champ en attente est tenu dans MainWindowViewModel.cs, hors du partiel.")
print("    c. le champ qui attend part avant ClosePropertyEdit, EndPendingEdit, un changement de sélection, l'enregistrement et l'export ; le partiel du poste")

# ------------------------------------------------------------- d. la barre d'état

exige("if (TopLevel.GetTopLevel(this)?.DataContext is MainWindowViewModel vm) vm.Status = message;" in champ,
      "le refus ne passe pas par la barre d'état du modèle de vue.")
exige("event" not in partiel, "le poste ajoute un canal au modèle de vue : la barre d'état (Status) suffit.")
print("    d. le refus se dit dans la barre d'état, par Status")

# ------------------------------------------------- e. les dialogues, et l'ordre de Tab

for dialogue in ("TemplateChooserWindow.axaml", "BusinessInfoWindow.axaml"):
    exige('<Button Content="Annuler" Click="OnCancel" MinWidth="90" IsCancel="True"/>' in lire(VUES / dialogue),
          f"{dialogue} n'a pas de bouton d'annulation : Échap ne le ferme pas.")
exige(re.search(r'<Style Selector="ToggleButton\.titreSection">\s*<Setter Property="IsTabStop" Value="False"/>', fenetre_axaml),
      "les titres de section du dock sont encore des arrêts de Tab.")
exige("if (e.Key == Key.Enter && champ.FindAncestorOfType<ChampNumerique>(includeSelf: true) is null) return;" in barre,
      "Entrée, dans la barre, ne rend pas la planche après un champ numérique.")
print("    e. Échap ferme les deux dialogues ; Tab saute les titres de section ; Entrée rend la planche dans la barre")

# ------------------------------------------------------------------- f. le README, I1

readme = lire(racine / "README.md")
exige("`mm`, `cm`, `pt`, `pi` (le pica) et le pouce (`po`, `in` ou `\"`)" in readme, "le README ne dit pas les unités lues.")
exige("renonce à ce qui est tapé et le quitte" in readme, "le tableau des raccourcis ne dit pas Échap dans un champ.")
exige("| Dans un champ numérique : `Entrée`, `Tab`, `↑` / `↓` |" in readme, "le tableau des raccourcis ne dit pas les touches d'un champ.")
exige("`Entrée` crée le document choisi, `Échap` renonce." in readme, "le README ne dit pas Échap au catalogue.")
exige("    MyPublisher/Views/ChampNumerique.cs\n" in lire(racine / "Scripts/recettes/I1-apparence.sh"),
      "ChampNumerique.cs n'est pas dans les vues de la phase (recette I1).")
print("    f. le README dit les unités, la validation, le refus et Échap ; ChampNumerique.cs est une vue de la phase")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : un ChampNumerique par champ, les unités lues, ce qui attend validé avant qu'on referme, le refus à la barre d'état, les dialogues, Tab, le README"

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

jouer "poste=N14" 12 "2. les champs, sur la vraie fenêtre : les unités, la valeur qui part une fois, le refus dit, Échap et Entrée, ce qui attend avant Annuler, Tab, les dialogues"

# Les voisins : les tests de la barre réécrits (règle 14), et le piège du plan.
voisins="FullyQualifiedName~BarreContextuelleTests.T_dans_le_corps_s_ecrit_et_T_apres_Echap_arme_l_outil_texte"
voisins+="|FullyQualifiedName~BarreContextuelleTests.Ctrl_U_pendant_une_saisie_de_la_barre_fait_son_propre_pas"
voisins+="|FullyQualifiedName~BarreContextuelleTests.Le_selecteur_de_couleur_referme_rend_les_lettres_aux_outils"
voisins+="|FullyQualifiedName~DispositionTests.Changer_d_onglet_referme_la_saisie_en_cours"
jouer "$voisins" 4 "   les voisins : Échap qui renonce, Entrée qui rend la planche (I3, réécrits) ; « 40 » gardé au changement d'onglet (I2)"

echo "N14 : les champs ne prennent ce qu'on tape qu'à la validation, lisent les autres unités, disent leurs refus, et Échap y renonce."
