#!/usr/bin/env bash
#
# Recette du poste N24 — exposant, indice et capitales (fiche 23, b14 ; spécifié le 25 octobre 2026,
# sur la décision 6 de la phase N). La barre Formatting de Publisher porte, absents par défaut,
# Exposant, Indice, Petites majuscules et Majuscules (p. 10) ; le modèle connaissait le décalage de
# ligne de base, que l'import remplissait, mais rien ne le posait, et les capitales n'existaient pas.
# Trois temps.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. le modèle : une clé « Capitales » — aucune, capitales, petites capitales — sur le bloc et
#         sur le fragment, additive : FormatVersion reste 5 (règle 15) ; copiée avec eux, portée par
#         le style effectif et par la mise en forme d'une plage (MiseEnForme) ; Samples et Templates
#         réenregistrés, chaque bloc de texte et chaque fragment l'écrivant ;
#      b. la composition : le Compositeur dessine les capitales, caractère pour caractère
#         (char.ToUpperInvariant), et fabrique les petites capitales à 70 % du corps ; ni l'écran
#         (TexteCompose) ni l'export (PdfExporter) ne mettent rien en capitales à part ;
#      c. l'import lit ce que libmspub écrit — fo:text-transform, fo:font-variant, et la taille
#         relative de style:text-position, appliquée au corps ;
#      d. les touches de Publisher en français : Ctrl+= l'indice, Ctrl+Maj+= l'exposant, Ctrl+Maj+K
#         les petites majuscules, liées une fois ; le zoom avant n'a plus de touche (règle 12), ses
#         entrées de menu non plus ; le README et la fiche 24 les disent ;
#      e. les menus : Texte ▸ Exposant et majuscules ▸ les quatre commandes, et leur pendant au menu
#         contextuel du texte ; les commandes au partiel du poste (règle 10) ; la planche
#         (PageCanvas.axaml.cs, PageCanvas.Saisie.cs) intacte.
#
# 2. L'IMAGE : un document qui porte un exposant, un indice, des majuscules et des petites
#    majuscules s'exporte ; le PDF dessine les capitales (pdftotext), le fichier garde le texte
#    tapé, et « comparer-rendus.sh » passe sur la page, à sa tolérance par défaut.
#
# 3. LES TESTS, sous le trait « poste=N24 » :
#
#      · le noyau (ExposantEtCapitalesTests) : les capitales dessinées sans toucher au texte, « ß »
#        tel quel, les positions de N3 ; un texte sans capitales composé comme avant ; les petites
#        capitales à 70 % du corps, la ligne de base d'un exposant gardée ; un mot en petites
#        capitales qui ne se coupe pas entre ses deux tailles ; le fragment qui donne ou retire les
#        capitales du bloc ; la mise en forme ; la clé additive et l'aller-retour ; l'import ;
#      · la vraie fenêtre (ExposantEtCapitalesSurLaFenetreTests) : la recette du plan — « x2 », le
#        « 2 » en exposant, au PDF au corps et à la ligne de base attendus, où l'écran le pose à
#        0,05 pt, en un pas — ; Ctrl+= qui met en indice et l'en retire, un pas chacun ; la
#        sélection mixte ; les petites majuscules à 0,1 pt de 70 % de la hauteur des capitales ; le
#        menu Texte et le menu du texte ; le zoom qui n'a plus Ctrl+=.
#
#    Le compte des tests joués est vérifié : un filtre qui ne désigne plus rien sort en 0.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"

if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

# ------------------------------------------------------------------------ 1. les sources

python3 - "$racine" <<'PY'
import json, pathlib, re, sys, xml.etree.ElementTree as ET

racine = pathlib.Path(sys.argv[1])
A = "{https://github.com/avaloniaui}"
X = "{http://schemas.microsoft.com/winfx/2006/xaml}"
echecs = []

def exige(condition, message):
    if not condition:
        echecs.append(message)

def lire(chemin):
    return (racine / chemin).read_text(encoding="utf-8")

def code(texte):
    """Le texte sans ses commentaires « /// » et « // »."""
    return re.sub(r"//.*", "", texte)

# a. le modèle.
fragment = lire("MyPublisher.Core/Models/StyleSpan.cs")
cadre = lire("MyPublisher.Core/Models/Frame.cs")
forme = lire("MyPublisher.Core/Models/MiseEnForme.cs")
document_cs = lire("MyPublisher.Core/Models/Document.cs")
exige(re.search(r"public enum Capitales\s*\{\s*(?:///.*\s*)*Aucune,\s*(?:///.*\s*)*Capitales,\s*(?:///.*\s*)*PetitesCapitales,", fragment),
      "l'énumération Capitales n'est pas « Aucune, Capitales, PetitesCapitales ».")
exige("private Capitales? _capitales;" in fragment and "Capitales = Capitales, Langue = Langue," in fragment,
      "le fragment ne porte pas des capitales nulles par défaut, ou sa copie les perd.")
exige("public Capitales Capitales { get; init; }" in fragment and "Capitales = fragment.Capitales ?? styles[i].Capitales," in fragment,
      "le style effectif ne porte pas les capitales du fragment.")
exige("private Capitales _capitales;" in cadre and "Foreground = Foreground, Align = Align,\n            Capitales = Capitales," in cadre,
      "le bloc de texte ne porte pas ses capitales, ou sa copie les perd.")
exige("return style with { Capitales = bloc.Capitales };" in lire("MyPublisher.Core/Models/Jeux.cs"),
      "la charte ne donne pas ses capitales au style commun d'un bloc.")
exige("Capitales = Commune(styles, s => s.Capitales)," in forme and "Sauf(Capitales, commun.Capitales)" in forme,
      "la mise en forme d'une plage ne lit ni ne normalise les capitales.")
exige("public const int CurrentFormatVersion = 5;" in document_cs, "FormatVersion n'est plus 5 (règle 15).")
blocs = fragments = 0
for chemin in sorted([*racine.glob("Samples/*.mypub"), *racine.glob("MyPublisher/Templates/*.mypub")]):
    document = json.loads(chemin.read_text(encoding="utf-8"))
    def parcours(bloc):
        global blocs, fragments
        if bloc.get("$kind") == "text":
            blocs += 1
            exige("Capitales" in bloc, f"{chemin.name} : « {bloc.get('Name')} » n'a pas été réenregistré (Capitales absent).")
        for f in bloc.get("Spans", []):
            fragments += 1
            exige("Capitales" in f, f"{chemin.name} : un fragment de « {bloc.get('Name')} » n'écrit pas ses capitales.")
        for enfant in bloc.get("Children", []):
            parcours(enfant)
        for case in bloc.get("Cells", []):
            for f in case.get("Spans", []):
                fragments += 1
                exige("Capitales" in f, f"{chemin.name} : un fragment de case n'écrit pas ses capitales.")
    for page in document["Pages"]:
        for bloc in page["Frames"]:
            parcours(bloc)
exige(blocs > 100, f"{blocs} blocs de texte dans Samples et Templates : le corpus a disparu ?")
print(f"    a. le modèle : la clé sur le bloc et le fragment, copiée, portée ; FormatVersion 5 ; "
      f"{blocs} blocs et {fragments} fragments réenregistrés")

# b. la composition, et elle seule.
compositeur = lire("MyPublisher.Core/Models/Compositeur.cs")
exige("public const double ReductionDesPetitesCapitales = 0.7;" in compositeur,
      "les petites capitales ne sont plus à 70 % du corps.")
exige("cible[i] = char.ToUpperInvariant(source[i]);" in compositeur,
      "la composition ne met plus les capitales caractère pour caractère.")
exige("foreach (var portion in EnCapitales(texte, debutDuMorceau, finDuMorceau, styleDuMorceau))" in compositeur,
      "la découpe ne passe plus par les portions en capitales.")
for vue in ("MyPublisher/Views/TexteCompose.cs", "MyPublisher.Core/Services/PdfExporter.cs"):
    exige(re.search(r"ToUpper|Capitales", code(lire(vue))) is None,
          f"{vue} met du texte en capitales à part : l'écran et le PDF doivent dessiner la même composition.")
print("    b. la composition : les capitales caractère pour caractère, les petites à 70 % ; ni l'écran ni le PDF à part")

# c. l'import.
importeur = lire("MyPublisher.Core/Services/PubImporter.cs")
exige('"fo:text-transform"' in importeur and '"uppercase"' in importeur and '"small-caps"' in importeur,
      "l'import ne lit pas les capitales de libmspub.")
exige("Points(corps) * echelle" in importeur, "l'import n'applique pas la taille relative de l'exposant au corps.")
print("    c. l'import : fo:text-transform, fo:font-variant, la taille relative au corps")

# d. les touches.
fenetre_txt = lire("MyPublisher/Views/MainWindow.axaml")
fenetre = ET.fromstring(fenetre_txt)
liaisons = [(e.get("Gesture"), e.get("Command")) for e in fenetre.iter(A + "KeyBinding")]
for geste, commande in (("Ctrl+OemPlus", "BasculerLIndiceCommand"), ("Ctrl+Shift+OemPlus", "BasculerLExposantCommand"),
                        ("Ctrl+Shift+K", "BasculerLesPetitesCapitalesCommand")):
    exige([c for g, c in liaisons if g == geste] == ["{Binding " + commande + "}"],
          f"« {geste} » n'est pas lié une fois, à {commande}.")
exige(not any(c == "{Binding ZoomInCommand}" for _, c in liaisons), "le zoom avant a encore une touche (règle 12).")
zooms = [e for e in fenetre.iter(A + "MenuItem") if e.get("Command") == "{Binding ZoomInCommand}"]
exige(len(zooms) == 2 and all(e.get("InputGesture") is None for e in zooms),
      "les deux entrées « Zoom avant » ne sont pas là, sans touche.")
exige('[Key.OemPlus] = "=",' in lire("MyPublisher/Views/GestesEnFrancais.cs"),
      "les menus n'écrivent pas « Ctrl+= » pour la touche « = ».")
readme = lire("README.md")
tableau = re.search(r"^## Raccourcis$(.*?)^## ", readme, re.S | re.M)
tableau = tableau.group(1) if tableau else ""
exige("| `Ctrl+=` / `Ctrl+Maj+=` | Indice / Exposant" in tableau and "| `Ctrl+Maj+K` | Petites majuscules" in tableau,
      "le tableau des raccourcis du README ne dit pas Ctrl+=, Ctrl+Maj+= et Ctrl+Maj+K.")
fiche = lire("MyPublisher/Docs/Publisher2007/24-raccourcis-clavier-francais.md")
for ligne in ("| Appliquer ou supprimer la mise en indice | Ctrl+= | HR | **lié** (N24",
              "| Appliquer ou supprimer la mise en exposant | Ctrl+Maj+= | HR | **lié** (N24",
              "| Ctrl+Maj+K | HR | **lié** (N24"):
    exige(ligne in fiche, f"la fiche 24 ne dit pas « {ligne[:60]}… ».")
print("    d. les touches : Ctrl+=, Ctrl+Maj+= et Ctrl+Maj+K liés une fois ; le zoom avant sans touche ; README et fiche 24")

# e. les menus, le partiel, la planche.
def sous_menu(parent, entete):
    trouves = [e for e in parent.iter(A + "MenuItem") if e.get("Header") == entete]
    return trouves[0] if len(trouves) == 1 else None
attendues = [("_Exposant", "Ctrl+Shift+OemPlus", "{Binding BasculerLExposantCommand}"),
             ("_Indice", "Ctrl+OemPlus", "{Binding BasculerLIndiceCommand}"),
             ("_Petites majuscules", "Ctrl+Shift+K", "{Binding BasculerLesPetitesCapitalesCommand}"),
             ("_Majuscules", None, "{Binding BasculerLesCapitalesCommand}")]
barres = [e for e in fenetre.iter(A + "Menu") if e.get(X + "Name") == "Menus"]
texte = sous_menu(barres[0], "_Texte") if barres else None
principal = sous_menu(texte, "Exposant et _majuscules") if texte is not None else None
contextuels = [e for e in fenetre.iter(A + "ContextMenu") if e.get(X + "Name") == "MenuDuBloc"]
contextuel = sous_menu(contextuels[0], "E_xposant et majuscules") if contextuels else None
for nom, menu in (("Texte ▸ Exposant et majuscules", principal), ("le menu du texte", contextuel)):
    exige(menu is not None, f"{nom} n'existe pas, une fois.")
    if menu is not None:
        entrees = [(e.get("Header"), e.get("InputGesture"), e.get("Command")) for e in menu if e.tag == A + "MenuItem"]
        exige(entrees == attendues, f"{nom} porte {entrees}.")
exige(contextuel is not None and contextuel.get("Classes") == "saisie blocs siTexte",
      "le sous-menu du menu contextuel ne paraît pas en saisie et sur un bloc de texte seul.")
partiel = lire("MyPublisher/ViewModels/MainWindowViewModel.ExposantEtCapitales.cs")
for commande in ("BasculerLExposant", "BasculerLIndice", "BasculerLesPetitesCapitales", "BasculerLesCapitales"):
    exige(re.search(r"\[RelayCommand\(CanExecute = nameof\(EstUnBlocDeTexte\)\)\]\s*private void " + commande + r"\(\)", partiel),
          f"la commande {commande} n'est pas au partiel du poste (règle 10).")
exige("public const double CorpsDOffice = 0.67;" in partiel and "public const double DecalageDOffice = 0.5;" in partiel,
      "l'exposant et l'indice ne posent plus le décalage et le corps d'Office.")
for planche in ("MyPublisher/Views/PageCanvas.axaml.cs", "MyPublisher/Views/PageCanvas.Saisie.cs"):
    exige(re.search(r"Capitales|Exposant|Indice\b", code(lire(planche))) is None,
          f"{planche} touche à l'exposant ou aux capitales (N25 y travaille).")
exige("*Texte ▸ Exposant et majuscules*" in readme, "le README ne dit pas le menu.")
print("    e. les menus : Texte ▸ Exposant et majuscules et le menu du texte, quatre entrées ; le partiel ; la planche intacte")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : le modèle et les documents, la composition, l'import, les touches, les menus"

# ------------------------------------------------------------------------ 2. l'image

# Un document de la recette R1, son bloc des indices récrit : un exposant, un indice, des
# majuscules, des petites majuscules, dans un bloc où rien d'autre ne change.
python3 - Samples/recette-texte-riche.mypub "$travail/n24.mypub" <<'PY'
import json, sys
document = json.load(open(sys.argv[1], encoding="utf-8"))
blocs = [f for p in document["Pages"] for f in p["Frames"] if f.get("$kind") == "text" and "H2O" in f.get("Text", "")]
if len(blocs) != 1:
    sys.exit("    le bloc « H2O » de recette-texte-riche n'est pas là, une fois")
bloc = blocs[0]
bloc["Text"] = "x2 et H2O ; Air conditioning, Atr Avion"
def fragment(debut, longueur, **valeurs):
    f = {"Start": debut, "Length": longueur, "FontFamily": None, "FontSize": None, "Bold": None, "Italic": None,
         "Underline": None, "Foreground": None, "BaselineShift": 0, "Capitales": None, "Langue": None}
    f.update(valeurs)
    return f
corps = bloc["FontSize"]
bloc["Spans"] = [fragment(1, 1, FontSize=corps * 0.67, BaselineShift=0.5),
                 fragment(7, 1, FontSize=corps * 0.67, BaselineShift=-0.5),
                 fragment(12, 16, Capitales="PetitesCapitales"),
                 fragment(30, 9, Capitales="Capitales")]
json.dump(document, open(sys.argv[2], "w", encoding="utf-8"), ensure_ascii=False, indent=2)
PY
"$binaire" "$travail/n24.mypub" --resave "$travail/relu.mypub" > /dev/null
if ! grep -q '"Text": "x2 et H2O ; Air conditioning, Atr Avion"' "$travail/relu.mypub"; then
    echo "ÉCHEC : le document réenregistré n'a plus le texte tapé." >&2
    exit 1
fi
"$binaire" "$travail/relu.mypub" --export "$travail/n24.pdf" > /dev/null
pdftotext "$travail/n24.pdf" "$travail/n24.txt"
for mot in "AIR CONDITIONING" "ATR AVION"; do
    if ! tr '\n' ' ' < "$travail/n24.txt" | grep -q "$mot"; then
        echo "ÉCHEC : le PDF ne dessine pas « $mot »." >&2
        sed 's/^/    /' "$travail/n24.txt" >&2
        exit 1
    fi
done
if ! Scripts/comparer-rendus.sh "$travail/relu.mypub" 1 > "$travail/comparaison.txt" 2>&1; then
    sed 's/^/    /' "$travail/comparaison.txt" >&2
    echo "ÉCHEC : l'écran et le PDF ne dessinent pas l'exposant et les capitales de même." >&2
    exit 1
fi
echo "  2. l'image : le PDF dessine les capitales, le fichier garde le texte tapé ; $(tail -1 "$travail/comparaison.txt")"

# ------------------------------------------------------------------------ 3. les tests

jouer() {
    local projet="$1" attendus="$2" quoi="$3"
    if ! dotnet test "$projet" ${MYPUBLISHER_DEJA_COMPILE:+--no-build} --nologo --filter "poste=N24" \
            > "$travail/tests.txt" 2>&1; then
        grep -E "Failed |Error Message|Assert" -A3 "$travail/tests.txt" | sed 's/^/    /' >&2 || sed 's/^/    /' "$travail/tests.txt" >&2
        echo "ÉCHEC : $quoi (« poste=N24 ») ne passe pas." >&2
        exit 1
    fi
    joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
    if [ "${joues:-0}" -lt "$attendus" ]; then
        echo "ÉCHEC : $attendus tests attendus sous « poste=N24 » ($quoi), ${joues:-0} joués." >&2
        echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
        exit 1
    fi
}

jouer MyPublisher.Tests/MyPublisher.Tests.csproj 8 "le noyau"
echo "  3. le noyau : $joues tests (les capitales sans toucher au texte ; les petites à 70 % ; le mot entier au renvoi ; la mise en forme ; la clé additive ; l'import)"

jouer MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj 6 "la vraie fenêtre"
echo "  4. la vraie fenêtre : $joues tests (le 2 de x2 en exposant au PDF comme à l'écran, en un pas ; l'indice posé et retiré ; la sélection mixte ; les petites majuscules à 70 % ; les menus ; le zoom)"

echo "N24 : l'exposant, l'indice, les majuscules et les petites majuscules se posent sur la sélection ou le bloc, en un pas, à l'écran comme au PDF ; le texte tapé ne change pas."
