#!/usr/bin/env bash
#
# Recette du poste N23 — puces et numérotation (fiche 23, b12 ; spécifié le 25 octobre 2026, sur la
# décision 6 de la phase N). Publisher met en liste par ses boutons Puces et Numérotation et par
# Format ▸ Bullets and Numbering… ; MyPublisher n'avait aucun moyen de faire une liste qui se
# renumérote. Une propriété de paragraphe, « Liste », sur le bloc et sur l'écart : aucune, des
# puces, ou des numéros « 1. », « a) », « i. » avec leur premier numéro. Deux temps.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. le modèle : la liste est une valeur (ListeDeParagraphe), sur TextFrame et sur
#         ParagraphSpan, clé additive — FormatVersion reste 5 (règle 15) —, copiée avec le bloc et
#         l'écart, et portée par la mise en forme d'une plage (MiseEnForme) ; Samples et Templates
#         réenregistrés, chaque bloc et chaque écart écrivant sa liste ; l'import ne la lit pas ;
#      b. la composition : la marque est un morceau sans position que le Compositeur pose, et lui
#         seul — ni l'écran (TexteCompose) ni l'export (PdfExporter) ne la dessinent à part : les
#         deux passent par la même composition ; le point d'insertion la saute (Abscisse) ;
#      c. la vue : la liste « Puces et numéros », la puce et le premier numéro dans la section
#         Paragraphe du dock, sur la ListeVisee du partiel du poste (règle 10) ; Texte ▸ Puces et
#         numéros… y mène (N18) ; Entrée prise dans la saisie (PageCanvas.Saisie.cs), et la planche
#         principale (PageCanvas.axaml.cs) intacte ; le README.
#
# 2. LES TESTS, sous le trait « poste=N23 » :
#
#      · le noyau (PucesEtNumerosTests) : « 1. » et « 2. » sur le 2e et le 3e de trois paragraphes,
#        au retrait de première ligne, le texte au retrait suspendu ; la suite interrompue par un
#        paragraphe hors liste, repartie au premier numéro ; les trois formes ; la marque sans
#        position, le curseur et le clic qui la sautent ; l'élément vide ; le style du premier
#        caractère ; le saut tapé qui continue la liste (Q1) ; le retrait suspendu donné et rendu ;
#        l'aller-retour JSON, et un document sans liste qui écrit des listes nulles ;
#      · la vraie fenêtre (PucesEtNumerosSurLaFenetreTests) : la recette du plan — les numéros
#        posés au dock sur le 2e et le 3e paragraphe, « 1. » et « 2. » au PDF au retrait de
#        première ligne (pdftotext -bbox), l'écran égal au PDF à 0,05 pt, un pas ; Entrée à la fin
#        de l'élément 2 donne « 3. », en un pas ; Entrée dans l'élément vide le sort de la liste ;
#        le menu, le bloc hors saisie, la puce tapée, le premier numéro, l'état mixte ; le clic sur
#        la marque et les caractères spéciaux (N3, N22) ; le compte des lignes en excédent (I9).
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
import json, pathlib, re, sys, xml.etree.ElementTree as ET

racine = pathlib.Path(sys.argv[1])
A = "{https://github.com/avaloniaui}"
X = "{http://schemas.microsoft.com/winfx/2006/xaml}"
VIEWS = "{using:MyPublisher.Views}"
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
liste = lire("MyPublisher.Core/Models/ListeDeParagraphe.cs")
cadre = lire("MyPublisher.Core/Models/Frame.cs")
ecart = lire("MyPublisher.Core/Models/ParagraphSpan.cs")
forme = lire("MyPublisher.Core/Models/MiseEnForme.cs")
document = lire("MyPublisher.Core/Models/Document.cs")
exige("public sealed record ListeDeParagraphe" in liste, "la liste n'est pas une valeur (record ListeDeParagraphe).")
exige(re.search(r"\[property: JsonPropertyOrder\(285\)\]\s*private ListeDeParagraphe\? _liste;", cadre) is not None,
      "le bloc de texte ne porte pas sa liste (TextFrame.Liste).")
exige(re.search(r"\[property: JsonPropertyOrder\(97\)\]\s*private ListeDeParagraphe\? _liste;", ecart) is not None,
      "l'écart de paragraphe ne porte pas sa liste (ParagraphSpan.Liste).")
exige("Liste = Liste" in code(cadre) and "Align = Align, Liste = Liste," in code(ecart),
      "la copie du bloc ou de l'écart oublie la liste.")
exige("ListeDeParagraphe? Liste = null" in forme and "e.Align, e.Liste)" in forme,
      "la mise en forme d'une plage (MiseEnForme) ne porte pas la liste des écarts : elle la perdrait en les récrivant.")
exige("public const int CurrentFormatVersion = 5;" in document, "FormatVersion n'est plus 5 (règle 15).")
exige(re.search(r"ListeDeParagraphe|Listes\.|\bListe\s*=", code(lire("MyPublisher.Core/Services/PubImporter.cs"))) is None,
      "l'import touche aux listes, ce que N23 ne fait pas.")
documents = sorted((racine / "Samples").glob("*.mypub")) + sorted((racine / "MyPublisher/Templates").glob("*.mypub"))
blocs = ecarts = 0
for chemin in documents:
    def parcourir(noeud):
        global blocs, ecarts
        if isinstance(noeud, dict):
            if noeud.get("$kind") == "text":
                blocs += 1
                exige("Liste" in noeud, f"{chemin.name} : un bloc de texte n'écrit pas sa liste (--resave).")
            if "IndentFirstLine" in noeud and "Start" in noeud and "$kind" not in noeud:
                ecarts += 1
                exige("Liste" in noeud, f"{chemin.name} : un écart de paragraphe n'écrit pas sa liste (--resave).")
            for valeur in noeud.values():
                parcourir(valeur)
        elif isinstance(noeud, list):
            for valeur in noeud:
                parcourir(valeur)
    parcourir(json.loads(chemin.read_text(encoding="utf-8")))
exige(blocs > 0, "aucun bloc de texte lu dans Samples et Templates : la lecture des documents a changé.")
print(f"    a. le modèle : la liste au bloc et à l'écart, FormatVersion 5, l'import intact ; "
      f"{len(documents)} documents réenregistrés, {blocs} blocs et {ecarts} écarts qui l'écrivent")

# b. la composition.
compositeur = lire("MyPublisher.Core/Models/Compositeur.cs")
exige("Listes.Marques(reglages)" in compositeur and "Morceau.SansPosition" in compositeur,
      "le Compositeur ne pose pas la marque des listes, en morceau sans position.")
exige(re.search(r"if \(morceau\.EstUneMarque\) continue;\s*var dedans = position - morceau\.Debut;", compositeur) is not None,
      "le point d'insertion ne saute pas la marque (Compositeur.Abscisse).")
for chemin in ("MyPublisher.Core/Services/PdfExporter.cs", "MyPublisher/Views/TexteCompose.cs", "MyPublisher/Views/ControleDeBloc.cs"):
    exige("Listes." not in code(lire(chemin)) and "Marques(" not in code(lire(chemin)),
          f"{chemin} dessine les marques à part : l'écran et le PDF doivent passer par la même composition.")
print("    b. la composition : la marque, morceau sans position, posée par le Compositeur seul ; le curseur la saute")

# c. la vue.
partiel = lire("MyPublisher/ViewModels/MainWindowViewModel.Listes.cs")
exige("public sealed class ListeVisee" in partiel and "public partial class MainWindowViewModel" in partiel,
      "la ListeVisee n'est pas dans le partiel du poste (règle 10).")
exige("_vise.Ecrire(" in partiel and "Listes.Poser(" in partiel and "Listes.PoserSurLeBloc(" in partiel,
      "la ListeVisee n'écrit pas par le ParagrapheVise, en écarts en saisie et sur le bloc hors saisie.")
fenetre = ET.fromstring(lire("MyPublisher/Views/MainWindow.axaml"))
sections = [e for e in fenetre.iter() if e.get(X + "Name") == "ContenuParagraphe"]
exige(len(sections) == 1, "la section Paragraphe du dock (« ContenuParagraphe ») n'est pas là une fois.")
if sections:
    section = sections[0]
    nommes = {e.get(X + "Name"): e for e in section.iter() if e.get(X + "Name")}
    choix = nommes.get("ListeDuParagraphe")
    exige(choix is not None and choix.tag == A + "ComboBox" and choix.get("SelectedItem") == "{Binding Option}",
          "« Puces et numéros » n'est pas une liste du dock liée à l'option de la ListeVisee.")
    exige(nommes.get("PuceDuParagraphe") is not None and nommes["PuceDuParagraphe"].get("Text") == "{Binding Puce}",
          "la puce n'est pas un champ du dock lié à la ListeVisee.")
    exige(nommes.get("PremierNumero") is not None and nommes["PremierNumero"].tag == VIEWS + "ChampNumerique"
          and nommes["PremierNumero"].get("Value") == "{Binding PremierNumero}",
          "le premier numéro n'est pas un champ numérique du dock lié à la ListeVisee.")
    contextes = [e for e in section.iter() if e.get(X + "DataType") == "vm:ListeVisee"]
    exige(len(contextes) == 3 and all("ListeVisee}" in (e.get("DataContext") or "") for e in contextes),
          "les trois champs des listes ne prennent pas la ListeVisee pour contexte.")
menus = [e for e in fenetre.iter(A + "MenuItem") if e.get(X + "Name") == "MenuPucesEtNumeros"]
exige(len(menus) == 1 and menus[0].get("Header") == "P_uces et numéros…"
      and menus[0].get("Command") == "{Binding PucesEtNumerosCommand}",
      "Texte ▸ Puces et numéros… n'est pas au menu, sur sa commande.")
exige('"ContenuParagraphe", "ListeDuParagraphe"' in partiel, "l'entrée « … » ne mène pas à la liste du dock (N18).")
saisie = lire("MyPublisher/Views/PageCanvas.Saisie.cs")
exige("e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None && SortirDeLaListe(zone)" in saisie,
      "Entrée n'est pas prise dans la saisie pour sortir d'un élément vide (PageCanvas.Saisie.cs).")
exige(re.search(r"ListeDeParagraphe|Listes\.|SortirDeLaListe", code(lire("MyPublisher/Views/PageCanvas.axaml.cs"))) is None,
      "la planche principale touche aux listes (règle 16).")
readme = lire("README.md")
exige("**Puces et numérotation**" in readme and "*Texte ▸ Puces et numéros…*" in readme,
      "le README ne dit pas les puces et la numérotation, ni l'entrée du menu.")
print("    c. la vue : la liste, la puce et le premier numéro au dock, sur la ListeVisee ; le menu ; Entrée dans la saisie ; le README")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : le modèle et les documents, la composition, la vue"

# ------------------------------------------------------------------------ 2. les tests

jouer() {
    local projet="$1" attendus="$2" quoi="$3"
    if ! dotnet test "$projet" ${MYPUBLISHER_DEJA_COMPILE:+--no-build} --nologo --filter "poste=N23" \
            > "$travail/tests.txt" 2>&1; then
        grep -E "Failed |Error Message|Assert" -A3 "$travail/tests.txt" | sed 's/^/    /' >&2 || sed 's/^/    /' "$travail/tests.txt" >&2
        echo "ÉCHEC : $quoi (« poste=N23 ») ne passe pas." >&2
        exit 1
    fi
    joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
    if [ "${joues:-0}" -lt "$attendus" ]; then
        echo "ÉCHEC : $attendus tests attendus sous « poste=N23 » ($quoi), ${joues:-0} joués." >&2
        echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
        exit 1
    fi
}

jouer MyPublisher.Tests/MyPublisher.Tests.csproj 24 "le noyau"
echo "  2. le noyau : $joues tests (la numérotation, ses interruptions et ses formes ; la marque sans position ; la frappe ; la pose ; le fichier)"

jouer MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj 10 "la vraie fenêtre"
echo "  3. la vraie fenêtre : $joues tests (1. et 2. au PDF comme à l'écran, en un pas ; Entrée qui continue et qui sort ; le menu, le bloc, la puce, le premier numéro, l'état mixte ; le clic sur la marque ; l'excédent)"

echo "N23 : les paragraphes se mettent en liste à puces ou numérotée, la marque au retrait de première ligne, à l'écran comme au PDF ; Entrée continue la liste, et en sort dans un élément vide."
