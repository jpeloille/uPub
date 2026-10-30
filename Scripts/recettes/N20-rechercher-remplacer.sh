#!/usr/bin/env bash
#
# Recette du poste N20 — rechercher et remplacer (fiche 23, e23 ; décision 6 de la phase N).
#
# Publisher le fait par Edition ▸ Rechercher et Remplacer, sur Ctrl+F, F3, Maj+F4 et Ctrl+H dans
# l'aide en français (fiche 24), touches réservées jusqu'au poste. MyPublisher pose une barre sur
# la planche, en haut à droite : le texte cherché, le texte de remplacement, « Respecter la
# casse », « Mot entier », Suivant, Remplacer, Remplacer tout. Le texte trouvé est sélectionné en
# saisie (N12, N15), la page suivant ; Remplacer tout fait un seul pas, par le noyau. Deux temps.
#
# 1. LES SOURCES, lues par un script :
#
#      a. la barre est une vue à elle (BarreDeRecherche.axaml), posée par la fenêtre dans la case
#         de la planche, par-dessus, en haut à droite — elle ne la déplace pas (règle 3) —, et
#         non dans la planche ni dans PageRoot (règle 4) ; elle est une vue de la phase (I1) ;
#      b. les quatre touches sont liées, chacune une fois, sur les commandes du partiel du poste
#         (règle 10), le menu Edition affiche Ctrl+F et Ctrl+H, le README les dit dans le
#         sous-tableau « Édition », la fiche 24 les dit liées ;
#      c. le remplacement passe par le noyau — Recherche.Remplacer, qui recale par une édition
#         dite (Recalage) —, jamais par la propriété Saisie que l'écran lie ; Remplacer tout
#         remplace tout dans UN geste, sans pas par occurrence ; le noyau n'a pas Avalonia ;
#      d. la planche ouvre la saisie du texte trouvé depuis son partiel de la saisie, et
#         PageCanvas.axaml.cs n'est pas touché (règle 16) ; la barre ferme à Échap et rend la
#         planche.
#
# 2. LES TESTS :
#
#      · le noyau (« RechercheTests », trait « poste=N20 ») : les occurrences sans
#        chevauchement, la casse, les accents, le mot entier, la plage qui est une occurrence ;
#        le texte remplacé qui prend le style de son premier caractère, un remplacement qui
#        efface ou raccourcit, une case, et le remplacement qui contient ce qu'on cherche ;
#      · la vraie fenêtre (« RechercherRemplacerTests », trait « poste=N20 ») : sur l'exemple,
#        « texte » trouvé à chaque F3, sélectionné en saisie à la bonne plage, la page suivant,
#        Maj+F4 qui repart du début ; « bulletin » trouvé depuis la page 2 au sous-titre de la
#        page 1 ; Remplacer tout « bulletin » par « journal » en un pas, les fragments gardés, et
#        une annulation qui rend le document — la garde du juge ; sur ATA 21, le compte des
#        occurrences égal à celui d'une recherche sur le texte des blocs, puis « Air » remplacé
#        à la casse, le gras du titre gardé, en un pas ; la casse, le mot entier, l'ordre de la
#        page ; Remplacer, un pas puis la suivante ; un groupe et une case ; Échap qui ferme la
#        barre et rend les lettres aux outils ; la barre qui ne déplace pas la planche ; la
#        recherche qui part du curseur.
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

fenetre = lire(VUES / "MainWindow.axaml")
barre = lire(VUES / "BarreDeRecherche.axaml")
barre_code = lire(VUES / "BarreDeRecherche.axaml.cs")
partiel = lire(VM / "MainWindowViewModel.Rechercher.cs")
noyau = lire(racine / "MyPublisher.Core/Models/Recherche.cs")
recalage = lire(racine / "MyPublisher.Core/Models/Recalage.cs")
saisie = lire(VUES / "PageCanvas.Saisie.cs")
planche = lire(VUES / "PageCanvas.axaml.cs")
planche_axaml = lire(VUES / "PageCanvas.axaml")

# ------------------------------------------------- a. une barre posée sur la planche

exige('x:Class="MyPublisher.Views.BarreDeRecherche"' in barre, "la barre de recherche n'est pas une vue à elle.")
for nom in ("TexteCherche", "TexteDeRemplacement", "RespecterLaCasse", "MotEntier",
            "BoutonSuivant", "BoutonRemplacer", "BoutonRemplacerTout", "BoutonFermer"):
    exige(f'x:Name="{nom}"' in barre, f"la barre n'a pas « {nom} ».")
pose = re.search(r'<views:PageCanvas Grid\.Column="2" x:Name="PageView"/>\s*(?:<!--.*?-->\s*)?'
                 r'<views:BarreDeRecherche Grid\.Column="2" x:Name="BarreDeRecherche"\s+'
                 r'HorizontalAlignment="Right" VerticalAlignment="Top"', fenetre, re.S)
exige(pose is not None, "la barre n'est pas posée dans la case de la planche, par-dessus, en haut à droite.")
exige('IsVisible="{Binding RechercheVisible}"' in fenetre, "la barre ne suit pas RechercheVisible.")
exige("BarreDeRecherche" not in planche_axaml and "BarreDeRecherche" not in planche,
      "la barre est dans la planche : elle y serait sous la mise à l'échelle, et près de PageRoot.")
i1 = lire(racine / "Scripts/recettes/I1-apparence.sh")
exige("    MyPublisher/Views/BarreDeRecherche.axaml\n" in i1 and "    MyPublisher/Views/BarreDeRecherche.axaml.cs\n" in i1,
      "la barre n'est pas dans les vues de la phase (recette I1).")
print("    a. la barre est une vue à elle, posée par-dessus la planche, en haut à droite, hors de PageRoot ; une vue de la phase")

# ------------------------------------------------------------------ b. les touches

liaisons = {
    "Ctrl+F": "OuvrirLaRechercheCommand",
    "Ctrl+H": "OuvrirLeRemplacementCommand",
    "F3": "RechercherLeSuivantCommand",
    "Shift+F4": "RechercherLeSuivantCommand",
}
for geste, commande in liaisons.items():
    exige(fenetre.count(f'<KeyBinding Gesture="{geste}" ') == 1, f"« {geste} » n'est pas lié une fois.")
    exige(f'<KeyBinding Gesture="{geste}" Command="{{Binding {commande}}}"/>' in fenetre,
          f"« {geste} » n'est pas lié à {commande}.")
    exige(re.search(r"\[RelayCommand\]\s*private void " + commande.removesuffix("Command") + r"\(\)", partiel),
          f"{commande} n'est pas au partiel du poste.")
exige(re.search(r'Header="Rec_hercher…" InputGesture="Ctrl\+F"\s+Command="\{Binding OuvrirLaRechercheCommand\}"', fenetre),
      "le menu Edition n'a pas « Rechercher… » sur Ctrl+F.")
exige(re.search(r'Header="Re_mplacer…" InputGesture="Ctrl\+H"\s+Command="\{Binding OuvrirLeRemplacementCommand\}"', fenetre),
      "le menu Edition n'a pas « Remplacer… » sur Ctrl+H.")
readme = lire(racine / "README.md")
edition = re.search(r"^### Édition$(.*?)^### ", readme, re.S | re.M)
edition = edition.group(1) if edition else ""
exige("| `Ctrl+F` / `Ctrl+H` | Rechercher / Remplacer" in edition, "le sous-tableau « Édition » du README ne dit pas Ctrl+F et Ctrl+H.")
exige("| `F3` ou `Maj+F4` | Rechercher le suivant" in edition, "le sous-tableau « Édition » du README ne dit pas F3 et Maj+F4.")
exige("| Dans la barre de recherche : `Entrée`, `Échap` |" in edition, "le README ne dit pas les touches de la barre.")
exige("- **Rechercher et remplacer**" in readme, "le README ne dit pas ce que l'application sait faire de plus.")
fiche = lire(racine / "MyPublisher/Docs/Publisher2007/24-raccourcis-clavier-francais.md")
exige(fiche.count("**lié** (N20)") == 2 and "**réservé** : N20" not in fiche, "la fiche 24 ne dit pas les touches liées par N20.")
print("    b. Ctrl+F, Ctrl+H, F3 et Maj+F4 liés une fois, au partiel ; le menu Edition, le README et la fiche 24 les disent")

# ------------------------------------------------------------- c. par le noyau

exige("using Avalonia" not in noyau, "la recherche du noyau dépend d'Avalonia.")
exige("Recalage.Appliquer(texte, apres, new Edition(plage.Start, plage.Length, par.Length), fragments, ecarts);" in noyau,
      "le remplacement ne recale pas les fragments par une édition dite.")
exige(re.search(r"public static void Appliquer\(\s*string avant, string apres, Edition edition,", recalage),
      "le recalage n'a pas d'entrée pour une édition dite.")
exige(not re.search(r"\.Saisie\b", re.sub(r"(?m)^\s*///.*$", "", partiel)),
      "le remplacement passe par la propriété Saisie, que l'écran lie.")
tout = corps(partiel, "private void RemplacerTout()") or ""
exige(tout.count("UnGesteUnPas(") == 1 and "RemplacerUneOccurrence" not in tout and "BeginEdit" not in tout,
      "Remplacer tout ne fait pas UN geste pour tout le document.")
exige("RemplacerLOccurrence(new Occurrence(cible, plage), par);" in tout, "Remplacer tout ne remplace plus par le noyau.")
exige(re.search(r"private void RemplacerUneOccurrence\(Occurrence occurrence, string par\) =>\s*UnGesteUnPas\(", partiel),
      "Remplacer ne fait pas son propre pas.")
print("    c. le remplacement passe par le noyau, par une édition dite, jamais par Saisie ; Remplacer tout, un seul geste")

# ------------------------------------------------- d. la planche, la barre, Échap

exige("TexteTrouve" not in planche, "PageCanvas.axaml.cs est touché (règle 16) : le texte trouvé est au partiel de la saisie.")
trouve = corps(saisie, "private void OnTexteTrouve(object? sender, SelectionDuTexte trouve)") or ""
exige("BeginTextEdit(bloc, trouve.Fin);" in trouve and "PoserLaSelection(zone," in trouve,
      "la planche n'ouvre pas la saisie du texte trouvé, la plage sélectionnée.")
touche = corps(barre_code, "private void SurTouche(object? sender, KeyEventArgs e)") or ""
exige(re.search(r"case Key\.Escape:\s*_vm\.FermerLaRechercheCommand\.Execute\(null\);\s*RendreLaPlanche\(\);", touche),
      "Échap ne ferme pas la barre en rendant la planche.")
exige("AddHandler(KeyDownEvent, SurTouche, RoutingStrategies.Tunnel);" in barre_code,
      "la barre ne prend pas Échap avant la fenêtre.")
print("    d. la planche ouvre la saisie du texte trouvé depuis son partiel ; Échap ferme la barre et rend la planche")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : la barre sur la planche, les quatre touches, le remplacement par le noyau en un geste, la planche"

# ------------------------------------------------------------------------ 2. les tests

jouer() {
    local projet="$1" filtre="$2" attendus="$3" quoi="$4"
    if ! dotnet test "$projet" ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
            --nologo -v q --filter "$filtre" > "$travail/tests.txt" 2>&1; then
        sed 's/^/    /' "$travail/tests.txt" >&2
        echo "ÉCHEC : les tests sous « $filtre » ne passent pas." >&2
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

jouer MyPublisher.Tests/MyPublisher.Tests.csproj "poste=N20" 9 \
    "2. le noyau : les occurrences, la casse, le mot entier, le texte remplacé au style de son premier caractère"
jouer MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj "poste=N20" 10 \
    "   la vraie fenêtre : F3 la page suivant, Remplacer tout en un pas, le compte d'ATA 21, la casse et le mot entier, Échap"

echo "N20 : la barre de recherche trouve, sélectionne en saisie et remplace par le noyau ; Remplacer tout fait un seul pas."
