#!/usr/bin/env bash
#
# Recette du poste N9 — la langue du texte. Le document porte toujours une langue, une
# étiquette BCP 47 ; un bloc de texte et un fragment de style peuvent la changer, nulle
# voulant dire « comme le bloc », puis « comme le document ». L'import d'un .pub la lit sur
# chaque fragment du relevé de libmspub ; le dock Document la règle en un pas ; un document
# vierge est en français, quelle que soit la machine ; rien de ce qui se dessine n'en
# dépend. Ce qu'elle prouve :
#
#   1. LES SOURCES, lues comme on les livre.
#      a. Les clés de langue du relevé sont celles que l'import lit : pub2raw écrit
#         « fo:language » et « fo:country » sur chaque fragment des deux documents réels —
#         ATA 21, 73 fragments « fr-FR » ; les Fiches, 298 « en-US » et 31 « fr-FR » —, et
#         PubImporter lit ces deux clés et « fo:script ».
#      b. La langue ne se dessine pas, par construction : StyleEffectif — ce que la
#         composition mesure et ce que les deux rendus dessinent — n'a pas de langue, et ni
#         le compositeur, ni l'export, ni aucune vue ne la lisent.
#      c. La langue d'un document vierge est une constante, « fr-FR » : ni Langues.cs ni
#         Document.cs ne lisent la culture ; FormatVersion reste à 5 (règle 15).
#      d. Le dock : une liste « LangueDuDocument » dans l'onglet Document, sur la propriété
#         du fichier partiel du poste (règle 10), qui ouvre et referme sa transaction.
#      e. Samples et Templates sont réenregistrés : chaque document écrit sa langue, chaque
#         bloc de texte et chaque fragment la leur ; relus et réécrits par « --resave », ils
#         ne changent pas d'un octet.
#   2. L'IMPORT, comparé à la SOURCE et non à lui-même : ATA 21 et les Fiches, ouverts par
#      le vrai binaire et réécrits en .mypub, sont relus caractère par caractère. Chaque
#      fragment du relevé qui porte un caractère se retrouve à sa place, dans son bloc ou
#      sa case, chaque caractère dans la langue déclarée ; les fragments qui ne portent que
#      leur marque de paragraphe n'ont rien à garder. ATA 21 : 50 fragments « fr-FR » sur
#      73 ; les Fiches : 294 « en-US » et 17 « fr-FR », sur 298 et 31.
#   3. LE FICHIER : l'import réécrit se relit et se réécrit à l'identique, chaque langue
#      gardée ; un fichier qui n'écrit pas de langue s'ouvre en « fr-FR », sous « LC_ALL=C »
#      comme sous une locale anglaise ou allemande.
#   4. LE RENDU NE CHANGE PAS : les deux documents importés, et le document d'exemple où
#      l'on pose une langue à chaque bloc et à chaque fragment, sont rendus tels quels puis
#      avec toutes leurs langues changées. L'empreinte du banc (lignes et morceaux à la
#      règle de l'écran et à celle du PDF, ce que l'écran pose, les pixels de chaque page),
#      le PDF du vrai binaire et ses pixels sont les mêmes au bit près ;
#      « Scripts/comparer-rendus.sh » rend le même écart écran-PDF des deux côtés.
#   5. LES TESTS « poste=N9 » : ceux du noyau (« LangueTests » — l'import sur des relevés
#      écrits à la main, l'aller-retour, le document vierge sous trois cultures, la copie,
#      la frappe, la mise en forme d'une plage et Ctrl+Espace qui gardent la langue, la
#      composition qui l'ignore), joués sous deux locales ; ceux de la vraie fenêtre
#      (« LangueDuDocumentTests » — les deux documents réels comparés à leur relevé, le
#      dock en un pas sans un pixel de changé, deux choix de suite en deux pas, la langue
#      inconnue montrée telle quelle, le document neuf et ses modèles).
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

application="MyPublisher/bin/Debug/net10.0/MyPublisher"
banc="MyPublisher.Banc/bin/Debug/net10.0/MyPublisher.Banc"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

# Le lecteur du format fermé, cherché comme la recette R5 le cherche : sans lui, la recette
# ne peut rien affirmer des documents réels, et le dit.
outil="${MYPUBLISHER_PUB2RAW:-$(Scripts/outils-pub.sh --chemin 2>/dev/null || true)}"
if [ -z "$outil" ] || [ ! -x "$outil" ]; then
    echo "ÉCHEC : « pub2raw » est introuvable : sudo apt install libmspub-tools, ou Scripts/outils-pub.sh" >&2
    exit 1
fi
export MYPUBLISHER_PUB2RAW="$outil"
if [ -d "$racine/.outils/mspub/lib" ]; then
    export LD_LIBRARY_PATH="$racine/.outils/mspub/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
fi

ata21="MyPublisher/Books/ATA 21 - Air conditioning.pub"
fiches="MyPublisher/Books/Fiches limitations et procédures d'urgences.pub"
"$outil" "$ata21" > "$travail/ata21.releve"
"$outil" "$fiches" > "$travail/fiches.releve"

# ------------------------------------------------------------------------ 1. les sources

python3 -I - "$racine" "$travail" <<'PY'
import collections, pathlib, re, sys
import xml.etree.ElementTree as ET

racine, travail = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
echecs = []

def exige(condition, message):
    if not condition:
        echecs.append(message)

def lire(chemin):
    return (racine / chemin).read_text(encoding="utf-8")

# a. les clés du relevé, et ce que l'import en lit.
def langues_declarees(releve):
    compte = collections.Counter()
    # Sans « newline="" », Python lit chaque « \r » de « insertText (\r) » comme une fin de ligne.
    for ligne in open(releve, encoding="utf-8", errors="replace", newline="").read().split("\n"):
        if ligne.strip().startswith("openSpan"):
            cles = dict(re.findall(r"([a-z:-]+): ([^,)]+)", ligne))
            exige("fo:language" in cles, f"un fragment de {releve.name} n'a pas de « fo:language » : {ligne.strip()[:80]}")
            compte["-".join(v for v in (cles.get("fo:language"), cles.get("fo:script"), cles.get("fo:country")) if v)] += 1
    return dict(compte)

exige(langues_declarees(travail / "ata21.releve") == {"fr-FR": 73},
      f"ATA 21 ne déclare plus 73 fragments fr-FR : {langues_declarees(travail / 'ata21.releve')}")
exige(langues_declarees(travail / "fiches.releve") == {"en-US": 298, "fr-FR": 31},
      f"les Fiches ne déclarent plus 298 en-US et 31 fr-FR : {langues_declarees(travail / 'fiches.releve')}")
importeur = lire("MyPublisher.Core/Services/PubImporter.cs")
for cle in ("fo:language", "fo:script", "fo:country"):
    exige(f'"{cle}"' in importeur, f"PubImporter ne lit pas « {cle} ».")
print("    a. pub2raw écrit fo:language et fo:country : ATA 21, 73 fr-FR ; les Fiches, 298 en-US et 31 fr-FR — l'import lit ces clés")

# b. la langue ne se dessine pas.
style = lire("MyPublisher.Core/Models/StyleSpan.cs")
effectif = re.search(r"public readonly record struct StyleEffectif\((.*?)\)", style, re.S)
exige(effectif is not None and "Langue" not in effectif.group(1),
      "StyleEffectif porte une langue : la composition la verrait.")
dessin = [racine / "MyPublisher.Core/Models/Compositeur.cs", racine / "MyPublisher.Core/Models/Jeux.cs"]
dessin += sorted((racine / "MyPublisher.Core/Services").glob("PdfExporter*.cs"))
dessin += sorted(p for p in (racine / "MyPublisher/Views").glob("*.cs"))
for fichier in dessin:
    exige(not re.search(r"\bLangues?\b", fichier.read_text(encoding="utf-8")),
          f"{fichier.relative_to(racine)} lit la langue : elle n'a rien à y faire.")
print(f"    b. StyleEffectif sans langue ; ni le compositeur, ni l'export, ni les {len(dessin) - 3} fichiers de code des vues ne la lisent")

# c. le document vierge, le format.
langues = lire("MyPublisher.Core/Models/Langues.cs")
document = lire("MyPublisher.Core/Models/Document.cs")
exige('public const string ParDefaut = "fr-FR";' in langues, "la langue par défaut n'est plus la constante « fr-FR ».")
exige("private string _langue = Langues.ParDefaut;" in document, "la langue d'un document neuf ne vient pas de Langues.ParDefaut.")
for nom, texte in (("Langues.cs", langues), ("Document.cs", document)):
    exige("CultureInfo" not in texte and "Globalization" not in texte, f"{nom} lit la culture : la langue du document en dépendrait.")
exige("public const int CurrentFormatVersion = 5;" in document, "FormatVersion n'est plus 5 (règle 15).")
print("    c. un document vierge est en « fr-FR », constante qui ne lit pas la culture ; FormatVersion 5")

# d. le dock.
A = "{https://github.com/avaloniaui}"
X = "{http://schemas.microsoft.com/winfx/2006/xaml}"
fenetre = ET.fromstring(lire("MyPublisher/Views/MainWindow.axaml"))
onglets = [e for e in fenetre.iter(A + "TabItem") if e.get(X + "Name") == "OngletDocument"]
exige(len(onglets) == 1, "l'onglet Document n'est pas dans la fenêtre, une fois.")
listes = [e for e in fenetre.iter(A + "ComboBox") if e.get(X + "Name") == "LangueDuDocument"]
exige(len(listes) == 1 and onglets and listes[0] in list(onglets[0].iter()),
      "la liste « LangueDuDocument » n'est pas dans l'onglet Document, une fois.")
if listes:
    exige(listes[0].get("SelectedItem") == "{Binding LangueDuDocument}"
          and listes[0].get("ItemsSource") == "{Binding LanguesProposees}",
          "la liste des langues n'est pas liée à LangueDuDocument et LanguesProposees.")
partiel = lire("MyPublisher/ViewModels/MainWindowViewModel.Langue.cs")
setter = re.search(r"public LangueProposee\? LangueDuDocument\s*\{(.*?)\n    \}", partiel, re.S)
exige(setter is not None and re.search(r"BeginEdit\(\);\s*Document\.Langue = value\.Etiquette;\s*CommitEdit\(\);", setter.group(1)),
      "le choix de la langue n'ouvre pas et ne referme pas sa transaction, dans le partiel du poste.")
print("    d. la liste LangueDuDocument dans l'onglet Document, son pas dans MainWindowViewModel.Langue.cs")

# e. Samples et Templates écrivent la langue partout.
import json
fichiers = sorted((racine / "Samples").glob("*.mypub")) + sorted((racine / "MyPublisher/Templates").glob("*.mypub"))
blocs = fragments = 0
def parcours(bloc, nom):
    global blocs, fragments
    if bloc["$kind"] == "text":
        blocs += 1
        exige("Langue" in bloc, f"{nom} : un bloc de texte n'écrit pas sa langue.")
        for f in bloc.get("Spans", []):
            fragments += 1
            exige("Langue" in f, f"{nom} : un fragment n'écrit pas sa langue.")
    for case in bloc.get("Cells", []):
        for f in case.get("Spans", []):
            fragments += 1
            exige("Langue" in f, f"{nom} : un fragment de case n'écrit pas sa langue.")
    for enfant in bloc.get("Children", []):
        parcours(enfant, nom)
for fichier in fichiers:
    doc = json.loads(fichier.read_text(encoding="utf-8"))
    exige(doc.get("Langue") == "fr-FR", f"{fichier.name} n'écrit pas « Langue »: « fr-FR ».")
    exige(doc.get("FormatVersion") == 5, f"{fichier.name} n'est pas au format 5.")
    for page in doc["Pages"]:
        for bloc in page["Frames"]:
            parcours(bloc, fichier.name)
exige(blocs > 0 and fragments > 0, "aucun bloc ni fragment dans Samples et Templates : le contrôle n'a rien lu.")
print(f"    e. {len(fichiers)} documents de Samples et Templates écrivent leur langue, et celle de {blocs} blocs et {fragments} fragments")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY

mkdir -p "$travail/resave"
for fichier in Samples/*.mypub MyPublisher/Templates/*.mypub; do
    "$application" "$fichier" --resave "$travail/resave/copie.mypub" > /dev/null
    if ! cmp -s "$fichier" "$travail/resave/copie.mypub"; then
        echo "ÉCHEC : $fichier, relu et réécrit, change : il n'a pas été réenregistré." >&2
        diff -u "$fichier" "$travail/resave/copie.mypub" | head -20 >&2
        exit 1
    fi
done
echo "  1. les sources : les clés de pub2raw, une langue qui ne se dessine pas, « fr-FR » sans la culture, le dock, Samples et Templates réenregistrés"

# ------------------------------------------------------------------------ 2. l'import

"$application" "$ata21" --resave "$travail/ata21.mypub" > /dev/null
"$application" "$fiches" --resave "$travail/fiches.mypub" > /dev/null

python3 -I - "$travail" <<'PY'
import collections, json, pathlib, re, sys

travail = pathlib.Path(sys.argv[1])
echec = False

def source(releve):
    """Les fragments du relevé : leur cible — un bloc de la page, ou une case —, leur texte
    tel que l'import le pose, leur langue déclarée."""
    fragments, bloc, tableau, case, langue, texte = [], -1, -1, None, None, None
    # « newline="" » : la marque de paragraphe « insertText (\r) » n'est pas une fin de ligne.
    for ligne in open(releve, encoding="utf-8", errors="replace", newline="").read().split("\n"):
        nu = ligne.strip()
        appel = nu.split("(")[0].strip()
        interieur = nu[nu.find("(") + 1:nu.rfind(")")] if "(" in nu else ""
        if appel == "startTableObject":
            tableau += 1
        elif appel == "openTableCell":
            cles = dict(re.findall(r"([a-z:-]+): ([^,)]+)", interieur))
            case = ("case", tableau, int(cles["librevenge:row"]), int(cles["librevenge:column"]))
        elif appel == "closeTableCell":
            case = None
        elif appel == "startTextObject" and case is None:
            bloc += 1
        elif appel == "openSpan":
            cles = dict(re.findall(r"([a-z:-]+): ([^,)]+)", interieur))
            langue = "-".join(v for v in (cles.get("fo:language"), cles.get("fo:script"), cles.get("fo:country")) if v) or None
            texte = []
        elif appel == "insertText" and texte is not None:
            texte.append(interieur.replace("\r", ""))
        elif appel == "insertTab" and texte is not None:
            texte.append("\t")
        elif appel == "insertLineBreak" and texte is not None:
            texte.append("\n")
        elif appel == "closeSpan" and texte is not None:
            fragments.append((case or ("bloc", bloc), "".join(texte), langue))
            texte = None
    return fragments

def langue_des_fragments(spans, longueur, i):
    langue = None
    for f in spans:
        debut = min(max(f["Start"], 0), longueur)
        fin = min(max(f["Start"] + f["Length"], debut), longueur)
        if f.get("Langue") is not None and debut <= i < fin:
            langue = f["Langue"]
    return langue

def cibles(document):
    """Le texte de chaque bloc et de chaque case, et la langue de chacun de ses caractères,
    à la règle du modèle : le fragment, puis le bloc, puis le document."""
    blocs, tableaux = [], []
    for page in document["Pages"]:
        for bloc in page["Frames"]:
            if bloc["$kind"] == "text":
                blocs.append(bloc)
            elif bloc["$kind"] == "table":
                tableaux.append(bloc)
    def cible(nom):
        if nom[0] == "bloc":
            b = blocs[nom[1]]
            return b["Text"], lambda i: langue_des_fragments(b["Spans"], len(b["Text"]), i) or b["Langue"] or document["Langue"]
        c = next(c for c in tableaux[nom[1]]["Cells"] if c["Row"] == nom[2] and c["Column"] == nom[3])
        return c["Text"], lambda i: langue_des_fragments(c["Spans"], len(c["Text"]), i) or document["Langue"]
    return cible

for nom, attendu in (("ata21", {"fr-FR": 50}), ("fiches", {"en-US": 294, "fr-FR": 17})):
    fragments = source(travail / f"{nom}.releve")
    document = json.load(open(travail / f"{nom}.mypub", encoding="utf-8"))
    cible = cibles(document)
    curseurs, gardees, perdues = collections.Counter(), collections.Counter(), []
    for ou, texte, langue in fragments:
        if not texte:
            continue
        contenu, langue_de = cible(ou)
        debut = contenu.find(texte, curseurs[ou])
        if debut < 0:
            perdues.append(f"« {texte[:40]} » introuvable dans {ou}")
            continue
        curseurs[ou] = debut + len(texte)
        lues = {langue_de(i) for i in range(debut, debut + len(texte))}
        if lues != {langue}:
            perdues.append(f"« {texte[:40]} », déclaré {langue}, importé en {sorted(lues)}")
        else:
            gardees[langue] += 1
    declarees = collections.Counter(l for _, _, l in fragments)
    print(f"    {nom:7} déclarés {dict(sorted(declarees.items()))}, gardés {dict(sorted(gardees.items()))}, "
          f"document en {document['Langue']}")
    for perte in perdues[:5]:
        print(f"    ÉCHEC : {perte}", file=sys.stderr)
    if perdues or dict(gardees) != attendu:
        print(f"    ÉCHEC : {nom} garde {dict(gardees)}, {attendu} attendus.", file=sys.stderr)
        echec = True

sys.exit(1 if echec else 0)
PY
echo "  2. l'import : chaque fragment des deux documents réels garde, caractère par caractère, la langue que libmspub lui donne"

# ------------------------------------------------------------------------ 3. le fichier

for nom in ata21 fiches; do
    "$application" "$travail/$nom.mypub" --resave "$travail/$nom-relu.mypub" > /dev/null
    if ! cmp -s "$travail/$nom.mypub" "$travail/$nom-relu.mypub"; then
        echo "ÉCHEC : $nom importé, relu puis réécrit, change." >&2
        diff -u "$travail/$nom.mypub" "$travail/$nom-relu.mypub" | head -20 >&2
        exit 1
    fi
done

# Un fichier d'avant N9 : le document d'exemple, toutes ses langues ôtées.
python3 -I - Samples/exemple-bulletin.mypub "$travail/ancien.mypub" <<'PY'
import json, sys
def oter(noeud):
    if isinstance(noeud, dict):
        noeud.pop("Langue", None)
        for valeur in noeud.values():
            oter(valeur)
    elif isinstance(noeud, list):
        for valeur in noeud:
            oter(valeur)
document = json.load(open(sys.argv[1], encoding="utf-8"))
oter(document)
json.dump(document, open(sys.argv[2], "w", encoding="utf-8"), ensure_ascii=False, indent=2)
PY
for locale in C en_US.UTF-8 de_DE.UTF-8; do
    LC_ALL="$locale" LANG="$locale" "$application" "$travail/ancien.mypub" --resave "$travail/ancien-$locale.mypub" > /dev/null
    langue="$(python3 -I -c 'import json, sys; print(json.load(open(sys.argv[1], encoding="utf-8"))["Langue"])' "$travail/ancien-$locale.mypub")"
    if [ "$langue" != "fr-FR" ]; then
        echo "ÉCHEC : sous LC_ALL=$locale, un fichier sans langue s'ouvre en « $langue »." >&2
        exit 1
    fi
done
if ! cmp -s "$travail/ancien-C.mypub" "$travail/ancien-en_US.UTF-8.mypub" ||
   ! cmp -s "$travail/ancien-C.mypub" "$travail/ancien-de_DE.UTF-8.mypub"; then
    echo "ÉCHEC : le même fichier ne se réécrit pas pareil sous trois locales." >&2
    exit 1
fi
echo "  3. le fichier : l'import réécrit est un point fixe ; un fichier sans langue s'ouvre en fr-FR sous C, en_US et de_DE"

# ------------------------------------------------------------------------ 4. le rendu

# Le document d'exemple, une langue posée à chaque bloc et à chaque fragment ; puis chacun
# des trois documents, toutes ses langues changées.
python3 -I - "$travail" Samples/exemple-bulletin.mypub <<'PY'
import json, pathlib, sys
travail, exemple = pathlib.Path(sys.argv[1]), sys.argv[2]

def poser(noeud, langue):
    if isinstance(noeud, dict):
        if "Langue" in noeud:
            noeud["Langue"] = langue
        for valeur in noeud.values():
            poser(valeur, langue)
    elif isinstance(noeud, list):
        for valeur in noeud:
            poser(valeur, langue)

document = json.load(open(exemple, encoding="utf-8"))
poser(document, "en-GB")
json.dump(document, open(travail / "exemple.mypub", "w", encoding="utf-8"), ensure_ascii=False, indent=2)

for nom in ("exemple", "ata21", "fiches"):
    document = json.load(open(travail / f"{nom}.mypub", encoding="utf-8"))
    poser(document, "ja-JP")
    json.dump(document, open(travail / f"{nom}-autre.mypub", "w", encoding="utf-8"), ensure_ascii=False, indent=2)
PY
# Le document d'exemple garde ses images à côté de lui : ses deux variantes y sont rendues.
cp "$travail/exemple.mypub" "Samples/.n9-exemple.mypub"
cp "$travail/exemple-autre.mypub" "Samples/.n9-exemple-autre.mypub"
trap 'rm -rf "$travail" Samples/.n9-exemple.mypub Samples/.n9-exemple-autre.mypub' EXIT

rendre() {
    local document="$1" sortie="$2"
    mkdir -p "$sortie"
    "$banc" empreinte "$document" --sortie "$sortie/empreinte.txt" > /dev/null
    env -u DISPLAY -u WAYLAND_DISPLAY "$application" "$document" --date 2026-09-06 --export "$sortie/sortie.pdf" > /dev/null 2>&1
    pdftoppm -r 144 -png "$sortie/sortie.pdf" "$sortie/pdf"
    pdftotext -bbox "$sortie/sortie.pdf" - 2> /dev/null | grep -E '^ *<(page|word)' > "$sortie/mots.txt" || true
    rm "$sortie/sortie.pdf"
    Scripts/comparer-rendus.sh "$document" 1 100 > "$sortie/ecart.txt"
}

for paire in "exemple:Samples/.n9-exemple.mypub:Samples/.n9-exemple-autre.mypub" \
             "ata21:$travail/ata21.mypub:$travail/ata21-autre.mypub" \
             "fiches:$travail/fiches.mypub:$travail/fiches-autre.mypub"; do
    IFS=: read -r nom tel autre <<< "$paire"
    rendre "$tel" "$travail/rendu/$nom"
    rendre "$autre" "$travail/rendu/$nom-autre"
    if ! diff -r "$travail/rendu/$nom" "$travail/rendu/$nom-autre" > "$travail/ecart.diff"; then
        echo "ÉCHEC : $nom ne se rend pas pareil dans une autre langue." >&2
        head -20 "$travail/ecart.diff" >&2
        exit 1
    fi
    lignes="$(grep -c '^      ligne ' "$travail/rendu/$nom/empreinte.txt" || true)"
    pages="$(find "$travail/rendu/$nom" -name 'pdf*.png' | wc -l)"
    echo "    $nom : $lignes lignes composées, $pages page(s) au PDF, mêmes pixels ; comparer-rendus, $(cut -d: -f2- "$travail/rendu/$nom/ecart.txt" | cut -d'(' -f1 | xargs) des deux côtés"
done
echo "  4. le rendu : l'écran, le PDF et leurs pixels ne changent pas quand la langue change"

# ------------------------------------------------------------------------ 5. les tests

jouer() {
    local projet="$1" attendus="$2" locale="$3"
    if ! LC_ALL="$locale" LANG="$locale" dotnet test "$projet" ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
            --nologo -v q --filter "poste=N9" > "$travail/tests.txt" 2>&1; then
        sed 's/^/    /' "$travail/tests.txt" >&2
        echo "ÉCHEC : les tests « poste=N9 » de $projet ne passent pas sous LC_ALL=$locale." >&2
        exit 1
    fi
    joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
    if [ "${joues:-0}" -lt "$attendus" ]; then
        echo "ÉCHEC : $attendus tests attendus sous « poste=N9 » dans $projet, ${joues:-0} joués." >&2
        echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
        exit 1
    fi
}

jouer MyPublisher.Tests/MyPublisher.Tests.csproj 26 C
noyau="$joues"
jouer MyPublisher.Tests/MyPublisher.Tests.csproj 26 en_US.UTF-8
jouer MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj 6 C
echo "  5. les tests : $noyau du noyau, sous C et sous en_US ; $joues sur la vraie fenêtre"

echo "N9 : le texte a une langue — lue à l'import, gardée au fichier, réglée au dock en un pas, « fr-FR » pour un document vierge —, et rien de ce qui se dessine n'en dépend."
