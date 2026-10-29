#!/usr/bin/env bash
#
# Recette du poste N18 — les menus vers le dock, et les pages, avec A8 et A9 (fiche 23, b17, d16,
# e5, e8, e9, e12, e15, e16, e18, f17, f18). Trois temps.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. le chemin vers le dock : MainWindowViewModel.VersLeDock.cs (règle 10) lève une
#         DockDemande, que la fenêtre traite — onglet, section après la mise en page, focus ; les
#         sept entrées « … » des menus sont sur leur commande, chacune ouvrant sa section ;
#      b. les pages au clavier : Ctrl+Maj+N, Ctrl+Maj+U, Ctrl+Pg.Suiv, Ctrl+Pg.Préc, F5 et Ctrl+B
#         en liaisons, sur leurs commandes, affichés au menu Page ; Ctrl+N, dans OnKeyDown, ne
#         prend plus Maj ;
#      c. le numéro de page de la barre d'état est un champ (ChampDeLaPage), sous le nom de
#         toujours ; Entrée y lit la page au modèle de vue, qui refuse avec un message ;
#      d. A8 : le dialogue « Insérer des pages », son nombre, ses deux positions, ses trois
#         contenus, le numéro de la page à copier grisé tant que son option n'est pas choisie,
#         OK par défaut, Annuler par Échap ; l'insertion en un pas, au modèle de vue ;
#      e. A9 : « Faire pivoter ou retourner ▸ » au menu Disposition, ses deux quarts de tour et la
#         rotation personnalisée ; les deux boutons −90° et +90° du dock, sans icône ;
#      f. Insertion ▸ Numéro de page, Date, Coordonnées ▸ posent un bloc à champ (C4) ;
#      g. le README dit les touches à leur sous-tableau « Pages », la fiche 24 les dit liées, et
#         les deux vues neuves sont dans la liste de la recette I1.
#
# 2. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre :
#
#      · sous « poste=N18 » (MenusVersLeDockTests) : chaque entrée « … » ouvre la bonne section,
#        même dock replié, sans pas — la recette du plan ; Police, Paragraphe et Rotation
#        personnalisée grisés sans leur bloc ; « 3 » puis Entrée dans le numéro de page mène à la
#        page 3, « 99 » est refusé avec un message, F5 et Ctrl+B y mènent, Échap renonce ;
#        Ctrl+Pg.Suiv et Ctrl+Pg.Préc, même en saisie ; Ctrl+Maj+U en un pas, même en saisie ;
#        Ctrl+Maj+N ouvre le dialogue et non plus le catalogue ; les blocs à champ en un pas ;
#      · sous « poste=A8 » (InsererDesPagesTests) : la recette d'origine — 3 pages, la page 2
#        courante, 4 pages insérées avant, copiées de la page 1 : 7 pages, les pages 2 à 5 portant
#        autant de blocs que la page 1, en instances distinctes, les noms renumérotés, une seule
#        annulation —, la zone de texte égale à la zone des marges, le champ grisé ;
#      · sous « poste=A9 » (QuartDeTourTests) : 350° plus 90° donne 80°, 0° moins 90° donne
#        270°, chaque commande en un pas, au menu et au dock ; le dernier écrit le document d'une
#        image tournée d'un quart de tour, que la suite mesure.
#
# 3. L'IMAGE TOURNÉE (la recette d'origine de A9) : une image dont aucun quart de tour ne se
#    confond avec un autre — trois aplats et un coin — tournée au menu, puis « comparer-rendus.sh »
#    confronte l'écran au PDF sous la tolérance de 0,5 % (0,07 %
#    mesuré le 29 octobre 2026), et le document dit 90°, signé.
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

axaml = lire("MyPublisher/Views/MainWindow.axaml")
fenetre = ET.fromstring(axaml)
code = lire("MyPublisher/Views/MainWindow.axaml.cs")
partiel = lire("MyPublisher/ViewModels/MainWindowViewModel.VersLeDock.cs")
vm = lire("MyPublisher/ViewModels/MainWindowViewModel.cs")
readme = lire("README.md")

def corps(source, signature):
    """Le corps d'une méthode, de son accolade ouvrante à la fermante de même retrait."""
    m = re.search(re.escape(signature) + r"[^\n]*\n(\s*)\{\n(.*?)\n\1\}", source, re.S)
    return m.group(2) if m else ""

menus = {e.get("Header"): e for e in fenetre.iter(A + "MenuItem")}
def entree(entete, commande, geste=None):
    e = menus.get(entete)
    exige(e is not None, f"le menu n'a pas d'entrée « {entete} ».")
    if e is None:
        return
    exige(e.get("Command") == "{Binding " + commande + "}", f"« {entete} » n'est pas sur {commande}.")
    if geste:
        exige(e.get("InputGesture") == geste, f"« {entete} » n'affiche pas {geste}.")

# ------------------------------------------------------------------ a. le chemin vers le dock
exige("public partial class MainWindowViewModel" in partiel, "le partiel du poste, MainWindowViewModel.VersLeDock.cs, manque (règle 10).")
exige("public event EventHandler<DockDemande>? DockDemandee;" in partiel, "le modèle de vue ne lève pas de DockDemande.")
exige("public sealed record DockDemande(" in partiel, "DockDemande n'est pas déclarée.")
for commande, section, ouvrir in (
        ("ReperesDeMiseEnPage", "ContenuMarges", "SectionMargesOuverte = true;"),
        ("FormatDesReperes", "ContenuReperesDeRegle", "SectionReperesOuverte = true"),
        ("ArrierePlan", "ContenuFondDePage", "SectionFondDePageOuverte = true"),
        ("MiseEnPage", "ContenuFormat", "SectionFormatOuverte = true"),
        ("Paragraphe", "ContenuParagraphe", "SectionParagrapheOuverte = true"),
        ("RotationPersonnalisee", "ContenuPositionEtTaille", "SectionPositionOuverte = true")):
    m = re.search(r"private void " + commande + r"\(\) => AllerAuDock\((.*?)\);\n", partiel, re.S)
    exige(m is not None and f'"{section}"' in m.group(1) and ouvrir in m.group(1),
          f"{commande} n'ouvre pas sa section {section}.")
    exige(sum(1 for e in fenetre.iter() if e.get(X + "Name") == section) == 1, f"la section {section} n'est pas nommée, une fois.")
exige(re.search(r'private void Police\(\) => AllerAuDock\(\s*new DockDemande\(DestinationDuReglage\.BarreContextuelle, null, "PoliceDuBloc"\)',
                partiel) is not None, "Police… ne mène pas à la liste des polices de la barre.")
aller = corps(partiel, "private void AllerAuDock(DockDemande demande, Action ouvrirLaSection)")
exige("DockVisible = true;" in aller and aller.find("ouvrirLaSection();") < aller.find("DockDemandee?.Invoke(this, demande);"),
      "AllerAuDock ne rouvre pas le dock, ou lève la demande avant d'ouvrir la section.")
exige("_vm.DockDemandee += OnDockDemandee;" in code and "_vm.DockDemandee -= OnDockDemandee;" in code,
      "la fenêtre ne suit pas la DockDemande, ou ne la lâche pas.")
traiter = corps(code, "private void MettreLeFocusAuReglage(Control racine, DockDemande demande, int essais)")
exige("Dispatcher.UIThread.Post(" in traiter and "DispatcherPriority.Background" in traiter,
      "le champ ne se vise pas après la mise en page (le piège des gabarits gardés).")
exige("NotifierLesEntreesVersLeDock();" in corps(vm, "private void NotifySelection()"),
      "la disponibilité des entrées ne suit pas la sélection.")
entree("_Mise en page…", "MiseEnPageCommand")
entree("Repères de m_ise en page…", "ReperesDeMiseEnPageCommand")
entree("_Format…", "FormatDesReperesCommand")
entree("Arrière-p_lan…", "ArrierePlanCommand")
entree("_Police…", "PoliceCommand")
entree("Pa_ragraphe…", "ParagrapheCommand")
entree("Rotation _personnalisée…", "RotationPersonnaliseeCommand")
print("    a. sept entrées « … », chacune sur sa commande, qui ouvre sa section ; la DockDemande traitée après la mise en page")

# ------------------------------------------------------------------- b. les pages au clavier
liaisons = [(e.get("Gesture"), e.get("Command")) for e in fenetre.iter(A + "KeyBinding")]
for geste, commande in (("Ctrl+Shift+N", "InsererDesPagesCommand"), ("Ctrl+Shift+U", "DupliquerLaPageCommand"),
                        ("Ctrl+PageDown", "PageSuivanteCommand"), ("Ctrl+PageUp", "PagePrecedenteCommand"),
                        ("F5", "AtteindreLaPageCommand"), ("Ctrl+B", "AtteindreLaPageCommand")):
    exige((geste, "{Binding " + commande + "}") in liaisons, f"« {geste} » n'est pas lié à {commande}.")
entree("_Insérer des pages…", "InsererDesPagesCommand", "Ctrl+Shift+N")
entree("_Dupliquer la page", "DupliquerLaPageCommand", "Ctrl+Shift+U")
entree("Page s_uivante", "PageSuivanteCommand", "Ctrl+PageDown")
entree("Page p_récédente", "PagePrecedenteCommand", "Ctrl+PageUp")
entree("A_tteindre la page…", "AtteindreLaPageCommand", "F5")
exige("case Key.N when !e.KeyModifiers.HasFlag(KeyModifiers.Shift): OnNew(this, e);" in code,
      "OnKeyDown prend encore Ctrl+Maj+N pour Nouveau.")
dupliquer = corps(partiel, "private void DupliquerLaPage()")
exige(dupliquer.find("SetSelection([]);") >= 0 and dupliquer.find("SetSelection([]);") < dupliquer.find("UnGesteUnPas("),
      "Ctrl+Maj+U ne referme pas la saisie avant de faire son pas.")
print("    b. Ctrl+Maj+N, Ctrl+Maj+U, Ctrl+Pg.Suiv, Ctrl+Pg.Préc, F5 et Ctrl+B liés et affichés ; Ctrl+N sans Maj")

# ------------------------------------------------------------------- c. le numéro de page
champ = lire("MyPublisher/Views/ChampDeLaPage.axaml")
champ_cs = lire("MyPublisher/Views/ChampDeLaPage.axaml.cs")
exige('<views:ChampDeLaPage Grid.Column="4" x:Name="PageSurPages"' in axaml, "la barre d'état ne porte pas le champ de la page.")
exige('<TextBox x:Name="NumeroDePage"' in champ and '<TextBlock x:Name="TotalDesPages"' in champ,
      "le champ de la page n'a pas son numéro et son total.")
exige("_vm.AtteindreLaPage(NumeroDePage.Text)" in corps(champ_cs, "private void Valider()"),
      "Entrée dans le numéro de page ne lit pas la page au modèle de vue.")
atteindre = corps(partiel, "public bool AtteindreLaPage(string? saisie)")
exige(atteindre.count("Status = ") >= 2 and "return false;" in atteindre, "un numéro de page refusé ne le dit pas.")
print("    c. le numéro de page de la barre d'état est un champ ; un refus se dit")

# ------------------------------------------------------------------------------- d. A8
dialogue = lire("MyPublisher/Views/InsererDesPages.axaml")
for nom in ("NombreDeNouvellesPages", "AvantLaPage", "ApresLaPage", "PagesVierges", "UneZoneDeTexte", "DupliquerLesObjets", "PageACopier"):
    exige(f'x:Name="{nom}"' in dialogue, f"le dialogue Insérer des pages n'a pas « {nom} ».")
exige('IsEnabled="{Binding #DupliquerLesObjets.IsChecked}"' in dialogue, "la page à copier n'est pas grisée hors de son option.")
exige('IsDefault="True"' in dialogue and 'IsCancel="True"' in dialogue, "OK ou Annuler n'a pas sa touche.")
inserer = corps(partiel, "public void InsererDesPages(DemandeDInsertion demande)")
exige("UnGesteUnPas(" in inserer and "bloc.Clone()" in inserer and "RenumberPages();" in inserer,
      "l'insertion ne fait pas un pas, ne copie pas des instances distinctes, ou ne renumérote pas.")
exige("InsererDesPages.Demander(this," in code, "la fenêtre n'ouvre pas le dialogue.")
print("    d. A8 : le dialogue, ses options, le champ grisé ; l'insertion en un pas, des copies distinctes, renumérotées")

# ------------------------------------------------------------------------------- e. A9
pivoter = menus.get("_Faire pivoter ou retourner")
exige(pivoter is not None and [e.get("Header") for e in pivoter if e.tag == A + "MenuItem"] ==
      ["Faire pivoter à _gauche de 90°", "Faire pivoter à _droite de 90°", "Rotation _personnalisée…"],
      "Disposition ▸ Faire pivoter ou retourner n'a pas ses trois entrées.")
entree("Faire pivoter à _gauche de 90°", "PivoterAGaucheCommand")
entree("Faire pivoter à _droite de 90°", "PivoterADroiteCommand")
for nom, texte in (("BoutonPivoterAGauche", "−90°"), ("BoutonPivoterADroite", "+90°")):
    m = re.search(r'<Button x:Name="' + nom + r'" Classes="boutonDuDock" Content="([^"]*)"', axaml)
    exige(m is not None and m.group(1) == texte, f"le dock n'a pas son bouton « {texte} », sans icône.")
exige("public static double QuartDeTour(double angle, double pas) => ((angle + pas) % 360 + 360) % 360;" in partiel,
      "le quart de tour ne se ramène pas entre 0 et 360°.")
exige("UnGesteUnPas(" in corps(partiel, "private void Pivoter(double pas)"), "le quart de tour ne fait pas son pas.")
print("    e. A9 : Faire pivoter ou retourner ▸ et les boutons −90° et +90° du dock, sans icône")

# ---------------------------------------------------------------- f. les blocs à champ
for entete, sorte in (("_Numéro de page", "PageNumber"), ("_Date", "Date"), ("_Nom", "ContactName"),
                      ("_Société", "ContactOrganisation"), ("_Téléphone", "ContactPhone")):
    e = menus.get(entete)
    exige(e is not None and e.get("Command") == "{Binding InsererUnChampCommand}"
          and e.get("CommandParameter") == "{x:Static models:FieldKind." + sorte + "}",
          f"Insertion ▸ « {entete} » ne pose pas un bloc à champ {sorte}.")
print("    f. Insertion ▸ Numéro de page, Date, Coordonnées ▸ : un bloc à champ")

# ------------------------------------------------------------------- g. README, fiche 24, I1
tableau = re.search(r"^## Raccourcis$(.*?)^## ", readme, re.S | re.M)
pages = re.search(r"^### Pages$(.*?)^### ", tableau.group(1) if tableau else "", re.S | re.M)
exige(pages is not None, "le README n'a pas de sous-tableau « Pages » aux raccourcis.")
for ligne in ("| `Ctrl+Maj+N` |", "| `Ctrl+Maj+U` |", "| `Ctrl+Pg.Suiv` / `Ctrl+Pg.Préc` |", "| `F5` ou `Ctrl+B` |"):
    exige(pages is not None and ligne in pages.group(1), f"le sous-tableau « Pages » du README ne dit pas {ligne}.")
fiche = lire("MyPublisher/Docs/Publisher2007/24-raccourcis-clavier-francais.md")
for touche in ("Ctrl+Maj+N", "Ctrl+Maj+U", "Ctrl+Pg. suiv", "Ctrl+Pg. préc", "F5 ou Ctrl+B"):
    lignes = [l for l in fiche.splitlines() if l.startswith("| ") and f"| {touche} |" in l and "N18" in l]
    exige(lignes and all("**lié** (N18)" in l for l in lignes), f"la fiche 24 ne dit pas « {touche} » lié par N18.")
i1 = lire("Scripts/recettes/I1-apparence.sh")
for vue in ("MyPublisher/Views/ChampDeLaPage.axaml", "MyPublisher/Views/InsererDesPages.axaml"):
    exige(f"    {vue}\n" in i1, f"{vue} n'est pas dans vues_de_la_phase de la recette I1.")
print("    g. le README, au sous-tableau « Pages » ; la fiche 24 ; les deux vues neuves à la recette I1")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : le chemin vers le dock, les pages au clavier, le numéro de page, A8, A9, les blocs à champ, le README"

# ------------------------------------------------------------------------ 2. les gestes

# L'image de A9 : trois aplats et un coin, si bien qu'aucun quart de tour ne se confond avec un
# autre. 480 × 240 pixels pour un bloc de 240 × 120 pt : deux pixels par point, l'échelle de
# comparer-rendus. En vraies couleurs sur 8 bits : la palette qu'ImageMagick choisit seul pour
# quatre couleurs, l'export la refuse (« Unsupported image format »), et le PDF sortait sans image.
mkdir -p "$travail/quart"
magick -size 480x240 xc:'#2E5AAC' \
       -fill '#E8B33A' -draw 'rectangle 0,0 159,239' \
       -fill '#C0392B' -draw 'rectangle 320,0 479,119' \
       -fill '#FFFFFF' -draw 'rectangle 20,20 79,79' \
       -depth 8 -type TrueColor "PNG24:$travail/quart/motif.png"

jouer() {
    local filtre="$1" attendus="$2" quoi="$3"
    if ! MYPUBLISHER_RECETTE_QUART="$travail/quart" \
         dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
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

jouer "poste=N18" 7 "2. N18, sur la vraie fenêtre : les entrées « … » vers le dock, le numéro de page, les pages au clavier, les blocs à champ"
jouer "poste=A8" 3 "   A8 : quatre pages avant, copiées de la page 1, en un pas ; la zone de texte ; le champ grisé"
jouer "poste=A9" 4 "   A9 : 350° + 90° = 80°, 0° − 90° = 270°, en un pas, au menu et au dock"

# Les voisins réécrits (règle 14) : Disposition et Texte rangés, et la barre d'état qui lit le champ.
voisins="FullyQualifiedName~MenusDePublisherTests.Le_menu_Disposition_est_range_comme_Publisher"
voisins+="|FullyQualifiedName~MenusDePublisherTests.Chaque_niveau_de_menu_a_des_lettres_d_acces_uniques_et_sans_accent"
voisins+="|FullyQualifiedName~DispositionTests.La_barre_d_etat_dit_la_page_le_format_et_le_zoom"
voisins+="|FullyQualifiedName~DispositionTests.Le_menu_Texte_porte_le_gras_l_italique_les_alignements_et_l_import"
jouer "$voisins" 4 "   les voisins : Disposition et Texte rangés, les lettres d'accès des entrées neuves, la barre d'état"

# -------------------------------------------------------------------- 3. l'image tournée

document="$travail/quart/quart.mypub"
if [ ! -f "$document" ]; then
    echo "ÉCHEC : le geste de A9 n'a pas écrit son document ($document)." >&2
    exit 1
fi
python3 - "$document" <<'PY'
import json, sys
doc = json.load(open(sys.argv[1], encoding="utf-8"))
blocs = doc["Pages"][0]["Frames"]
if len(blocs) != 1 or abs(blocs[0].get("Rotation", 0) - 90) > 1e-9:
    sys.exit(f"    ÉCHEC : le document de A9 ne porte pas une image tournée de 90° ({[b.get('Rotation') for b in blocs]})")
print("    le document porte l'image tournée de +90°, signé")
PY
if ! Scripts/comparer-rendus.sh "$document" 1 0.5 > "$travail/rendus.txt" 2>&1; then
    sed 's/^/    /' "$travail/rendus.txt" >&2
    echo "ÉCHEC : l'image tournée d'un quart de tour ne se dessine pas à l'écran comme au PDF." >&2
    exit 1
fi
echo "  3. l'image tournée d'un quart de tour : $(tail -1 "$travail/rendus.txt")"

echo "N18 : les entrées « … » mènent au dock, les pages ont leurs touches et leur dialogue (A8), et le quart de tour son geste (A9)."
