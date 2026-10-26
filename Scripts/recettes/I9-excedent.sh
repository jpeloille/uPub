#!/usr/bin/env bash
#
# Recette du poste I9 — compter les lignes en excédent : l'alerte de la maquette au dock,
# section « Cadre de texte », « Texte en excédent : 4 lignes ». Trois temps.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. le compte est une fonction pure du noyau, « Compositeur.LignesEnExcedent », qui
#         mesure comme « Debordement » : le calage vertical, le bord du BLOC, le seuil d'un
#         demi-point ;
#      b. l'avertissement d'export garde sa formulation, au caractère près, et ne parle pas
#         de lignes ; le relevé de P2 ne bouge pas — c'est « controle.sh » qui le tient ;
#      c. ce que le poste ajoute au modèle de vue est dans son fichier partiel (règle 10) ;
#      d. l'alerte est dans la place que I2 lui a réservée, une fois, aux couleurs et à
#         l'icône de l'apparence, citées et non écrites, et la fenêtre est inscrite à la
#         liste des vues de la phase de la recette I1 ;
#      e. aucune clé sérialisée : le noyau ne nomme l'excédent que dans le compositeur, et
#         aucun document livré ne le porte.
#
# 2. LES TESTS, par leur trait « poste=I9 » : le compte pur (« ExcedentTests » — dix lignes
#    qui en montrent six en comptent quatre, zéro sans débordement, colonnes, marge
#    intérieure, calage, seuil, interligne serré) et la vraie fenêtre (« ExcedentEcranTests » — le compte de
#    chaque bloc du document de A2, le même à la règle de l'export, l'alerte qui suit la
#    frappe sans changement de sélection, les couleurs et l'icône de la maquette ; depuis
#    la relecture du 26 octobre 2026, les fragments, les écarts de paragraphe, le jeu de
#    polices et son annulation, le champ résolu et les coordonnées adoptées).
#
# 3. LE PDF CONTRE LE DOCK, sur « Samples/recette-debordement.mypub » puis sur une variante
#    écrite ici, pour ce que le document de A2 ne porte pas — deux colonnes, quatre marges
#    et le calage au milieu, un interligne lâche, des colonnes tournées. Pour chaque bloc
#    de texte, le banc lit l'alerte que la vraie fenêtre affiche quand on le sélectionne
#    (« MyPublisher.Banc excedent ») ; « pdftotext -raw -bbox » rend les mots du PDF dans
#    l'ordre où l'export les dessine, bloc après bloc, et la recette les rend à leur bloc
#    par leur texte. Une ligne est absente du PDF quand son encre sort du bloc : la découpe
#    la cache — « pdftotext » ne voit pas les découpes, c'est la boîte de chaque mot, rendue
#    au repère du bloc, tourné compris, qui le dit. Le compte du dock est celui des lignes
#    absentes, et il vaut ce que le document a été écrit pour donner : un compte faux des
#    deux côtés à la fois ne passe pas.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

document="Samples/recette-debordement.mypub"
binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"
banc="MyPublisher.Banc/bin/Debug/net10.0/MyPublisher.Banc"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

for outil in "$binaire" "$banc"; do
    if [ ! -x "$outil" ]; then
        echo "ÉCHEC : binaire introuvable ($outil). Compiler d'abord." >&2
        exit 1
    fi
done

# ------------------------------------------------------------------------ 1. les sources

python3 - "$racine" <<'PY'
import json, pathlib, re, sys
import xml.etree.ElementTree as ET

racine = pathlib.Path(sys.argv[1])
echecs = []

def exige(condition, message):
    if not condition:
        echecs.append(message)

def sources(dossier):
    return [p for p in (racine / dossier).rglob("*.cs") if "/obj/" not in str(p) and "/bin/" not in str(p)]

# a. la fonction pure du noyau.
compositeur = (racine / "MyPublisher.Core/Models/Compositeur.cs").read_text(encoding="utf-8")
corps = re.search(r"public static int LignesEnExcedent\(\s*IReadOnlyList<LigneComposee> lignes, double disponible, "
                  r"double margeBasse,\s*VerticalAlign fer\)\s*\{(.*?)\n    \}", compositeur, re.S)
exige(corps is not None, "« Compositeur.LignesEnExcedent(lignes, disponible, margeBasse, fer) » n'est pas dans le noyau.")
if corps:
    texte = corps.group(1)
    exige("Decalage(lignes, disponible, fer)" in texte, "le compte ne suit pas le calage vertical du noyau (A7).")
    exige("disponible + margeBasse + SeuilDebordement" in texte,
          "le compte ne mesure pas au bord du bloc, au seuil du marqueur (A2, C6).")
    exige(not re.search(r"\b(Avalonia|PdfSharp|XGraphics)\b", texte), "le compte n'est pas pur.")
print("    a. Compositeur.LignesEnExcedent : pure, au calage, au bord du bloc et au seuil de A2")

# b. l'avertissement d'export, au caractère près.
export = (racine / "MyPublisher.Core/Services/PdfExporter.cs").read_text(encoding="utf-8")
exige('$"{repere} : le texte déborde du bloc de " +' in export
      and '$"{debordement.ToString("0.#", Nombres)} pt — ce qui dépasse n\'est pas imprimé.");' in export,
      "l'avertissement de débordement de l'export a changé de formulation.")
exige("xcédent" not in export and "LignesEnExcedent" not in export,
      "l'export parle de lignes en excédent : son avertissement garde sa formulation.")
print("    b. l'avertissement d'export garde sa formulation")

# c. le partiel du modèle de vue.
partiel = racine / "MyPublisher/ViewModels/MainWindowViewModel.Excedent.cs"
exige(partiel.exists(), "ce que I9 ajoute au modèle de vue n'a pas son fichier partiel (règle 10).")
if partiel.exists():
    contenu = partiel.read_text(encoding="utf-8")
    exige("public partial class MainWindowViewModel" in contenu, "MainWindowViewModel.Excedent.cs n'est pas un partiel du modèle de vue.")
    for membre in ("public int LignesEnExcedent", "public bool TexteEnExcedent", "public string AlerteDExcedent",
                   "Compositeur.LignesEnExcedent("):
        exige(membre in contenu, f"« {membre} » n'est pas dans le partiel du poste.")
for fichier in sources("MyPublisher"):
    if fichier == partiel:
        continue
    exige(not re.search(r"\b(LignesEnExcedent|TexteEnExcedent|AlerteDExcedent)\b", fichier.read_text(encoding="utf-8")),
          f"{fichier.relative_to(racine)} déclare ou calcule l'excédent hors du partiel du poste.")
print("    c. le partiel MainWindowViewModel.Excedent.cs")

# d. l'alerte, à sa place, aux couleurs de l'apparence.
A = "{https://github.com/avaloniaui}"
X = "{http://schemas.microsoft.com/winfx/2006/xaml}"
fenetre = ET.parse(racine / "MyPublisher/Views/MainWindow.axaml").getroot()
places = [e for e in fenetre.iter() if e.get(X + "Name") == "PlaceDeLAlerteDExcedent"]
alertes = [e for e in fenetre.iter() if e.get(X + "Name") == "AlerteDExcedent"]
exige(len(places) == 1 and len(alertes) == 1, "l'alerte n'est pas, une fois, dans la place que I2 lui a réservée.")
if len(places) == 1 and len(alertes) == 1:
    alerte = alertes[0]
    exige(alerte in list(places[0]), "l'alerte n'est pas dans « PlaceDeLAlerteDExcedent ».")
    exige(alerte.tag == A + "Border", "l'alerte n'est pas un Border.")
    exige(alerte.get("Background") == "{StaticResource FondAlerte}", "le fond de l'alerte n'est pas celui de l'apparence.")
    exige(alerte.get("TextElement.Foreground") == "{StaticResource TexteAlerte}",
          "le texte de l'alerte n'a pas la couleur de l'apparence.")
    exige(alerte.get("CornerRadius") == "6", "l'alerte n'a pas le rayon 6 de la maquette.")
    exige((alerte.get("IsVisible") or "").endswith("DataContext).TexteEnExcedent}"),
          "l'alerte ne se montre pas sur « TexteEnExcedent » : zéro s'annoncerait.")
    exige(any(e.get("Content") == "{StaticResource IconeAlerte}" for e in alerte.iter()),
          "l'alerte ne porte pas l'icône d'alerte de l'apparence.")
    exige(any((e.get("Text") or "").endswith("DataContext).AlerteDExcedent}") for e in alerte.iter(A + "TextBlock")),
          "le texte de l'alerte n'est pas lié au libellé du modèle de vue.")
recette_i1 = (racine / "Scripts/recettes/I1-apparence.sh").read_text(encoding="utf-8")
liste = re.search(r"vues_de_la_phase=\((.*?)\)", recette_i1, re.S).group(1).split()
exige("MyPublisher/Views/MainWindow.axaml" in liste, "la fenêtre n'est pas dans la liste des vues de la phase de la recette I1.")
exige(re.search(r'"IconeAlerte\s+I9\s', recette_i1) is not None, "l'icône d'alerte n'est plus attribuée à I9 par la recette I1.")
print("    d. l'alerte dans sa place, fond, texte et icône de l'apparence ; la fenêtre inscrite à la recette I1")

# e. aucune clé sérialisée.
for fichier in sources("MyPublisher.Core"):
    if fichier.name == "Compositeur.cs":
        continue
    exige(not re.search(r"xc[eé]dent", fichier.read_text(encoding="utf-8"), re.I),
          f"{fichier.relative_to(racine)} nomme l'excédent : I9 n'ajoute rien au modèle.")
for livre in list((racine / "Samples").glob("*.mypub")) + list((racine / "MyPublisher/Templates").glob("*.mypub")):
    exige(not re.search(r"xc[eé]dent", livre.read_text(encoding="utf-8"), re.I),
          f"{livre.relative_to(racine)} porte une clé d'excédent.")
print("    e. aucune clé sérialisée : ni dans le modèle, ni dans les documents livrés")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : la fonction pure, l'avertissement intact, le partiel, l'alerte à sa place"

# ------------------------------------------------------------------------ 2. les tests

jouer() {   # <projet> <attendus> <libellé>
    if ! dotnet test "$1" ${MYPUBLISHER_DEJA_COMPILE:+--no-build} --nologo -v q --filter "poste=I9" \
            > "$travail/tests.txt" 2>&1; then
        sed 's/^/    /' "$travail/tests.txt" >&2
        echo "ÉCHEC : $3 (« poste=I9 ») ne passent pas." >&2
        exit 1
    fi
    local joues
    joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
    if [ "${joues:-0}" -lt "$2" ]; then
        echo "ÉCHEC : $2 tests attendus sous « poste=I9 » dans $1, ${joues:-0} joués." >&2
        echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
        exit 1
    fi
    echo "     $3 : $joues tests"
}
jouer MyPublisher.Tests/MyPublisher.Tests.csproj 15 "le compte pur"
jouer MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj 9 "la vraie fenêtre"
echo "  2. les tests du poste"

# ------------------------------------------------------------------------ 3. le PDF contre le dock

# La formulation de A2, sur le document de A2 : cinq avertissements, mot pour mot.
"$binaire" "$document" --export "$travail/recette.pdf" > /dev/null 2> "$travail/recette.txt"
for ligne in \
    "Trop plein : le texte déborde du bloc de 42,4 pt — ce qui dépasse n'est pas imprimé." \
    "Milieu qui deborde : le texte déborde du bloc de 42,4 pt — ce qui dépasse n'est pas imprimé." \
    "Haut qui deborde : le texte déborde du bloc de 42,4 pt — ce qui dépasse n'est pas imprimé." \
    "Trop plein pivote : le texte déborde du bloc de 42,4 pt — ce qui dépasse n'est pas imprimé." \
    "Marge basse depassee : le texte déborde du bloc de 1,2 pt — ce qui dépasse n'est pas imprimé."; do
    if ! grep -qxF "Avertissement : Page 1 · $ligne" "$travail/recette.txt"; then
        echo "ÉCHEC : l'export ne dit plus « Page 1 · $ligne »" >&2
        cat "$travail/recette.txt" >&2
        exit 1
    fi
done
echo "     l'export avertit comme avant : cinq blocs, mot pour mot"

# La variante : ce que le document de A2 ne porte pas, bâti sur ses blocs pour en garder
# toutes les clés.
python3 - "$document" "$travail/variante.mypub" <<'PY'
import copy, json, sys
source, cible = sys.argv[1], sys.argv[2]
document = json.load(open(source, encoding="utf-8"))
modele = document["Pages"][0]["Frames"][0]

def bloc(nom, x, y, largeur, hauteur, lignes, **reglages):
    b = copy.deepcopy(modele)
    b.update(Name=nom, X=x, Y=y, Width=largeur, Height=hauteur,
             Text="\n".join(f"Ligne {i}" for i in range(1, lignes + 1)))
    b.update(reglages)
    return b

marges = dict(PaddingLeft=4, PaddingTop=4, PaddingRight=4, PaddingBottom=4)
document["Title"] = "Recette I9 — variante"
document["Pages"] = [document["Pages"][0]]
document["Pages"][0]["Frames"] = [
    # Quatorze lignes en deux colonnes de 60 : quatre dans la première, dix dans la
    # seconde, dont quatre montrées.
    bloc("Deux colonnes", 56.69, 56.69, 300, 60, 14, Columns=2, ColumnGap=12),
    # Quatre marges de 4 et le calage au milieu, huit lignes : la zone en prend cinq, la
    # cinquième dans le bloc à 2,5 pt du bord ; trois manquent.
    bloc("Marges et milieu", 56.69, 160, 200, 70, 8, VerticalAlign="Middle", **marges),
    # Interligne 1,5 : trois lignes dans 55, la dernière dessinée jusqu'à 54,4 alors que
    # son avance descend à 57,6. A2 avertit ; aucune ligne ne manque.
    bloc("Interligne lache", 56.69, 260, 200, 55, 3, LineHeight=1.5),
    # Le même dans 50 : l'encre de la troisième est coupée.
    bloc("Interligne lache coupe", 300, 260, 200, 50, 3, LineHeight=1.5),
    # Deux colonnes tournées de 15° : huit lignes dans la seconde, quatre montrées.
    bloc("Colonnes tournees", 300, 420, 220, 60, 12, Columns=2, ColumnGap=12, Rotation=15),
]
json.dump(document, open(cible, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
PY

confronter() {   # <doc.mypub> <attendus « nom=n » séparés par des |>
    local doc="$1" nom
    nom="$(basename "$doc" .mypub)"
    "$binaire" "$doc" --export "$travail/$nom.pdf" > /dev/null 2>&1
    pdftotext -raw -bbox "$travail/$nom.pdf" "$travail/$nom.xml"
    "$banc" excedent "$doc" --sortie "$travail/$nom.json" > /dev/null
    python3 - "$travail/$nom.xml" "$travail/$nom.json" "$2" <<'PY'
import json, math, re, sys
import xml.etree.ElementTree as ET

boites, releve, attendus = sys.argv[1], sys.argv[2], sys.argv[3]
attendus = dict((a.split("=")[0], int(a.split("=")[1])) for a in attendus.split("|"))
SEUIL = 0.5            # Compositeur.SeuilDebordement
echecs = []

# Les mots, page par page, dans l'ordre du flux : celui où l'export les dessine.
racine = ET.parse(boites).getroot()
pages = []
for page in racine.iter():
    if page.tag.endswith("page"):
        pages.append([(w.text or "", *(float(w.get(k)) for k in ("xMin", "yMin", "xMax", "yMax")))
                      for w in page.iter() if w.tag.endswith("word")])

blocs = json.load(open(releve, encoding="utf-8"))
curseurs = [0] * len(pages)

for bloc in blocs:
    mots = pages[bloc["Page"] - 1]
    i = curseurs[bloc["Page"] - 1]
    voulu = re.sub(r"\s", "", bloc["Contenu"])
    lu, pris = "", []
    while len(lu) < len(voulu) and i < len(mots):
        lu += mots[i][0]
        pris.append(mots[i])
        i += 1
    curseurs[bloc["Page"] - 1] = i
    if lu != voulu:
        echecs.append(f"« {bloc['Nom']} » : le PDF ne rend pas son texte à sa place dans le flux ({lu[:30]!r}…).")
        continue

    # Chaque mot au repère du bloc : l'origine du crayon — « pdftotext » pose la boîte d'un
    # caractère tourné au crayon, montée au-dessus — ramenée par la rotation inverse autour
    # du centre du bloc. La montée vaut 0,8 de la boîte en DejaVu Sans ; une erreur d'un
    # point sur elle ne déplace le haut de la ligne que de 1 − cos θ point.
    theta = math.radians(bloc["Rotation"])
    cx, cy = bloc["X"] + bloc["Largeur"] / 2, bloc["Y"] + bloc["Hauteur"] / 2
    poses = []
    for (_, x0, y0, x1, y1) in pris:
        h = y1 - y0
        ox, oy = x0 - cx, y0 + 0.8 * h - cy
        bx = cx + ox * math.cos(theta) + oy * math.sin(theta) - bloc["X"]
        by = cy - ox * math.sin(theta) + oy * math.cos(theta) - bloc["Y"]
        poses.append((bx, by - 0.8 * h, by - 0.8 * h + h))

    # Les lignes : un nouveau haut, ou un retour en arrière — le passage d'une colonne à
    # la suivante remonte.
    lignes = []
    for (x, haut, bas) in poses:
        if lignes and abs(haut - lignes[-1]["haut"]) < 1 and x >= lignes[-1]["x"] - 1:
            lignes[-1]["bas"] = max(lignes[-1]["bas"], bas)
            lignes[-1]["x"] = x
        else:
            lignes.append({"haut": haut, "bas": bas, "x": x})

    bord = bloc["Hauteur"] + SEUIL
    for ligne in lignes:
        if abs(ligne["bas"] - bord) < 0.2:
            echecs.append(f"« {bloc['Nom']} » : une ligne finit à {ligne['bas']:.2f} pt, trop près du bord compté "
                          f"({bord:.2f}) pour rien prouver.")
    absentes = sum(1 for l in lignes if l["bas"] > bord)

    if bloc["Rotation"] and not abs(lignes[0]["haut"] - bloc["MargeHaute"]) < 0.3:
        echecs.append(f"« {bloc['Nom']} » : ramenée au repère du bloc, sa première ligne commence à "
                      f"{lignes[0]['haut']:.2f} pt et non au haut de sa zone.")

    alerte = bloc["Alerte"]
    if alerte is None:
        dock = 0
    else:
        m = re.fullmatch(r"Texte en excédent : (\d+) (lignes?)", alerte)
        if not m or int(m.group(1)) < 1 or (m.group(2) == "ligne") != (int(m.group(1)) == 1):
            echecs.append(f"« {bloc['Nom']} » : le dock affiche « {alerte} ».")
            continue
        dock = int(m.group(1))

    attendu = attendus.get(bloc["Nom"])
    print(f"      {bloc['Nom']:<24} dock {dock}   PDF {absentes} absente(s) sur {len(lignes)}")
    if dock != absentes:
        echecs.append(f"« {bloc['Nom']} » : le dock compte {dock} ligne(s) en excédent, le PDF en perd {absentes}.")
    if attendu is None:
        echecs.append(f"« {bloc['Nom']} » : aucun compte attendu n'est écrit pour ce bloc.")
    elif absentes != attendu:
        echecs.append(f"« {bloc['Nom']} » : {absentes} ligne(s) absente(s) du PDF, {attendu} attendue(s).")

if set(attendus) != {b["Nom"] for b in blocs}:
    echecs.append(f"les blocs relevés ne sont pas ceux qu'on attendait : {sorted(b['Nom'] for b in blocs)}")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
}

echo "     $document :"
confronter "$document" "Trop plein=4|Temoin qui tient=0|Calage haut=0|Calage milieu=0|Calage bas=0|Milieu qui deborde=4|Haut qui deborde=4|Retrait pendant=0|Trop plein pivote=4|Marge basse absorbe=0|Marge basse depassee=1"
echo "     la variante — colonnes, marges, calage, interligne, rotation :"
confronter "$travail/variante.mypub" "Deux colonnes=6|Marges et milieu=3|Interligne lache=0|Interligne lache coupe=1|Colonnes tournees=4"
echo "  3. le compte du dock est celui des lignes absentes du PDF, bloc par bloc"

echo "I9 : le dock compte les lignes que le bloc ne montre pas, par la fonction du noyau, comme le PDF les perd ; l'alerte suit la frappe et se tait à zéro."
