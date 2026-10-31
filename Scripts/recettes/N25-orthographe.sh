#!/usr/bin/env bash
#
# Recette du poste N25 — la vérification orthographique (fiche 23, a26 ; décision 6 de la phase N).
# Publisher souligne les fautes pendant la frappe, les propose au clic droit et les parcourt par
# F7 ; MyPublisher n'avait rien. Un service du noyau, Orthographe, rend les fautes d'un texte mot par
# mot, chaque mot dans sa langue (N9), avec un cache par mot, sur les dictionnaires de Hunspell du
# système lus par WeCantSpell.Hunspell ; la planche les souligne d'un trait ondulé dans l'Overlay,
# aux places de N3 ; F7 met la barre de N20 en mode orthographe ; le clic droit sur une faute met
# ses suggestions en tête du menu du texte (A12) ; le dictionnaire de l'utilisateur et la case
# « Vérifier pendant la frappe » sont dans les préférences (N11). Quatre temps.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. la dépendance : WeCantSpell.Hunspell 7.0.1, au noyau seul ; la licence du paquet restauré
#         est bien la triple licence de Hunspell (MPL 1.1, GPL 2.0, LGPL 2.1), et le README dit
#         celle que MyPublisher prend — la MPL 1.1 — et où est le code source ;
#      b. le service du noyau, sans Avalonia, sur /usr/share/hunspell ; donné par App.axaml.cs seul,
#         comme les préférences : aucun autre fichier de l'application ne lit les dictionnaires du
#         système ; les dictionnaires de test sont dans Scripts/fixtures/orthographe/ ;
#      c. F7 lié une fois, à la commande du partiel du poste (règle 10) ; au menu Edition avec la
#         case « Vérifier pendant la frappe » ; au README, sous-tableau Édition ; lié dans la fiche 24 ;
#      d. le trait : le contrôle des fautes posé dans l'Overlay, jamais dans PageRoot (règle 4) ; ni
#         l'export ni le fichier principal de la planche n'en savent rien ; sa couleur vient de
#         l'apparence, aucune n'est écrite en dur ; les vues neuves sont à la recette I1.
#
# 2. LE NOYAU, joué par « MyPublisher.Tests » (trait « poste=N25 » : OrthographeTests) : la langue
#    mène au fichier (fr-FR → fr_FR, puis fr) ; les mots, apostrophe et trait d'union compris ;
#    « recieve » faute, « receive » non ; sigles et chiffres hors vérification ; le cache par mot ; un
#    dictionnaire absent le dit une fois ; chaque mot d'un bloc dans sa langue ; Ignorer tout et
#    Ajouter ; les préférences relues.
#
# 3. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre (trait
#    « poste=N25 » : VerificationOrthographiqueTests) — la recette du plan, et ce qui l'entoure :
#
#      · avec le dictionnaire de test, « recieve » souligné et « receive » non, aux places de N3 ;
#      · Rendu.Encre de la page et le PDF identiques, vérification ouverte ou fermée ;
#      · F7 parcourt les fautes dans l'ordre de lecture, de page en page, et reprend au début ;
#        Échap rend le mot, Ctrl+F rend la recherche ;
#      · Remplacer fait un pas, Entrée dans les suggestions aussi ;
#      · un mot ajouté au dictionnaire survit à un redémarrage, Ignorer tout non ;
#      · les suggestions en tête du menu du clic droit, en un pas ;
#      · le mot qu'on tape ne se souligne qu'une fois quitté ; la case ferme tout ;
#      · le trait d'un bloc tourné tourne avec lui ; la langue de chaque mot ; un dictionnaire
#        absent le dit une fois ; sans correcteur, rien.
#
#    Le compte des tests joués est vérifié : un filtre qui ne désigne plus rien sort en 0.
#
# 4. LE RELEVÉ D'ATA 21 EN en-US, sur le dictionnaire du système (MyPublisher.Banc orthographe) :
#    le nombre de mots signalés, relevé trois fois — un correcteur neuf, le même cache plein, un
#    second correcteur — et stable. ATA 21 est écrit en anglais sous une étiquette fr-FR : le relevé
#    impose en-US. Sans /usr/share/hunspell/en_US.dic, la recette le dit et saute ce temps, sans
#    conclure.
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

python3 -I - "$racine" "${NUGET_PACKAGES:-$HOME/.nuget/packages}" <<'PY'
import json, pathlib, re, sys
import xml.etree.ElementTree as ET

racine, paquets = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
echecs = []

def exige(condition, message):
    if not condition:
        echecs.append(message)

def lire(chemin):
    chemin = racine / chemin
    return chemin.read_text(encoding="utf-8") if chemin.exists() else ""

def code(texte):
    """Le code sans ses commentaires : ce qui compile, et non ce qui en parle."""
    texte = re.sub(r"/\*.*?\*/", "", texte, flags=re.S)
    return re.sub(r"//[^\n]*", "", texte)

# a. la dépendance, et sa licence.
VERSION = "7.0.1"
noyau = lire("MyPublisher.Core/MyPublisher.Core.csproj")
exige(f'<PackageReference Include="WeCantSpell.Hunspell" Version="{VERSION}"/>' in noyau,
      f"le noyau ne tire pas WeCantSpell.Hunspell {VERSION}.")
for projet in ("MyPublisher/MyPublisher.csproj", "MyPublisher.Tests/MyPublisher.Tests.csproj",
               "MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj", "MyPublisher.Banc/MyPublisher.Banc.csproj"):
    exige("WeCantSpell" not in lire(projet), f"{projet} tire le correcteur : le noyau seul le porte.")
actifs = json.loads(lire("MyPublisher.Core/obj/project.assets.json") or "{}")
exige(f"WeCantSpell.Hunspell/{VERSION}" in actifs.get("libraries", {}),
      f"la restauration n'a pas résolu WeCantSpell.Hunspell {VERSION}.")
licence = paquets / "wecantspell.hunspell" / VERSION / "license.txt"
texte_licence = licence.read_text(encoding="utf-8") if licence.exists() else ""
exige("Version: MPL 1.1/GPL 2.0/LGPL 2.1" in texte_licence,
      f"la licence du paquet restauré ({licence}) n'est plus la triple licence de Hunspell : au propriétaire de trancher.")
readme = lire("README.md")
exige("MyPublisher l'emploie sous **MPL 1.1**, sans la modifier" in readme and
      "<https://github.com/aarondandy/WeCantSpell.Hunspell>, version 7.0.1" in readme,
      "le README ne dit pas la licence prise ni où est le code source de la bibliothèque.")
exige("sudo apt install hunspell-fr hunspell-en-us" in readme, "le README ne dit pas comment installer les dictionnaires.")
print(f"    a. WeCantSpell.Hunspell {VERSION} au noyau seul ; MPL 1.1/GPL 2.0/LGPL 2.1 dans le paquet ; la MPL 1.1 et la source au README")

# b. le service, et qui le donne.
service = lire("MyPublisher.Core/Services/Orthographe.cs")
exige(service != "" and "Avalonia" not in code(service), "le service du noyau est absent, ou parle d'Avalonia.")
exige('public const string DossierDuSysteme = "/usr/share/hunspell";' in service, "le service ne lit pas /usr/share/hunspell.")
app = lire("MyPublisher/App.axaml.cs")
exige(code(app).count("viewModel.Orthographe = Orthographe.DuSysteme();") == 1,
      "App.axaml.cs ne donne pas le correcteur du système au modèle de vue, une fois.")
for chemin in sorted(list((racine / "MyPublisher").rglob("*.cs")) + list((racine / "MyPublisher.Tests.Interface").rglob("*.cs"))):
    if {"bin", "obj"} & set(chemin.relative_to(racine).parts) or chemin.name == "App.axaml.cs":
        continue
    exige("DuSysteme()" not in code(chemin.read_text(encoding="utf-8")),
          f"{chemin.relative_to(racine)} lit les dictionnaires du système : seul App.axaml.cs le fait.")
fixtures = racine / "Scripts/fixtures/orthographe"
for nom in ("en_US.aff", "en_US.dic", "fr.aff", "fr.dic"):
    exige((fixtures / nom).exists(), f"le dictionnaire de test {nom} manque dans Scripts/fixtures/orthographe/.")
print("    b. le service du noyau sur /usr/share/hunspell, donné par App.axaml.cs seul ; les dictionnaires de test")

# c. F7, le menu, le README, la fiche 24.
A = "{https://github.com/avaloniaui}"
X = "{http://schemas.microsoft.com/winfx/2006/xaml}"
fenetre = ET.fromstring(lire("MyPublisher/Views/MainWindow.axaml"))
liaisons = [(e.get("Gesture"), e.get("Command")) for e in fenetre.iter(A + "KeyBinding")]
exige([c for g, c in liaisons if g == "F7"] == ["{Binding VerifierLOrthographeCommand}"], "F7 n'est pas lié une fois à la vérification.")
partiel = lire("MyPublisher/ViewModels/MainWindowViewModel.Orthographe.cs")
exige(re.search(r"\[RelayCommand\]\s*private void VerifierLOrthographe\(\)", partiel) is not None,
      "la commande de F7 n'est pas dans le partiel du poste (règle 10).")
edition = [e for e in fenetre.iter(A + "MenuItem") if e.get("Header") == "_Edition"]
entrees = {e.get(X + "Name"): e for e in edition[0]} if edition else {}
verifier, case = entrees.get("MenuVerifierLOrthographe"), entrees.get("MenuVerifierPendantLaFrappe")
exige(verifier is not None and verifier.get("InputGesture") == "F7" and verifier.get("Command") == "{Binding VerifierLOrthographeCommand}",
      "le menu Edition n'a pas « Vérifier l'orthographe… » sur F7.")
exige(case is not None and case.get("ToggleType") == "CheckBox" and
      case.get("IsChecked") == "{Binding VerifierPendantLaFrappe, Mode=TwoWay}",
      "le menu Edition n'a pas la case « Vérifier pendant la frappe ».")
section = re.search(r"^### Édition$(.*?)^### ", readme, re.S | re.M)
exige(section is not None and re.search(r"^\| `F7` \| Vérifier l'orthographe", section.group(1), re.M) is not None,
      "le sous-tableau Édition du README ne dit pas F7.")
fiche = lire("MyPublisher/Docs/Publisher2007/24-raccourcis-clavier-francais.md")
exige(re.search(r"^\| Vérifier l’orthographe \| F7 \| HR \| \*\*lié\*\* \(N25\)", fiche, re.M) is not None,
      "la fiche 24 ne dit pas F7 lié par N25.")
print("    c. F7 lié une fois, au menu Edition avec la case, au README et à la fiche 24")

# d. le trait, dans l'Overlay.
suivi = lire("MyPublisher/Views/PageCanvas.CaracteresSpeciaux.cs")
exige("Overlay.Children.Insert(0, _fautesSoulignees);" in suivi, "le contrôle des fautes n'est pas posé dans l'Overlay.")
exige(re.search(r"PageRoot\.Children\.(Add|Insert)", suivi + lire("MyPublisher/Views/PageCanvas.Orthographe.cs")) is None,
      "le poste pose quelque chose dans PageRoot, que l'écran compare au PDF.")
planche_axaml = lire("MyPublisher/Views/PageCanvas.axaml")
racine_page = re.search(r'<Panel Name="PageRoot"(.*?)</LayoutTransformControl>', planche_axaml, re.S)
exige(racine_page is not None and "FautesSoulignees" not in racine_page.group(1), "les fautes sont dans PageRoot.")
planche = lire("MyPublisher/Views/PageCanvas.axaml.cs")
exige("FautesSoulignees" not in planche and "Orthographe" not in planche,
      "le fichier principal de la planche parle des fautes (règle 16).")
for exportateur in sorted((racine / "MyPublisher.Core/Services").glob("PdfExporter*.cs")):
    exige("Orthographe" not in exportateur.read_text(encoding="utf-8") and "Faute" not in exportateur.read_text(encoding="utf-8"),
          f"{exportateur.name} parle des fautes : rien ne s'en imprime.")
style = re.search(r'<Style Selector="views\|FautesSoulignees">(.*?)</Style>', planche_axaml, re.S)
exige(style is not None and '<Setter Property="Encre" Value="{StaticResource ' in style.group(1),
      "le trait ne prend pas sa couleur à l'apparence.")
for nom in ("MyPublisher/Views/FautesSoulignees.cs", "MyPublisher/Views/PageCanvas.Orthographe.cs",
            "MyPublisher/Views/PageCanvas.CaracteresSpeciaux.cs", "MyPublisher/Views/BarreDeRecherche.axaml"):
    texte = lire(nom)
    exige(texte != "", f"{nom} est introuvable.")
    exige(re.search(r"#[0-9A-Fa-f]{6}\b|Colors\.|Brushes\.", code(texte)) is None, f"{nom} écrit une couleur en dur.")
exige("geometrie.AbscisseSur(posee, debut)" in suivi and "geometrie.AbscisseSur(posee, fin)" in suivi,
      "le trait n'est pas posé sur la géométrie du texte dessiné (N3).")
recette_i1 = lire("Scripts/recettes/I1-apparence.sh")
liste = re.search(r"vues_de_la_phase=\((.*?)\)", recette_i1, re.S).group(1).split()
for vue in ("MyPublisher/Views/FautesSoulignees.cs", "MyPublisher/Views/PageCanvas.Orthographe.cs"):
    exige(vue in liste, f"{vue} n'est pas dans la liste des vues de la phase de la recette I1.")
print("    d. le trait dans l'Overlay, hors de PageRoot, de l'export et du fichier principal ; la couleur de l'apparence ; la recette I1")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : la dépendance et sa licence, le service, F7 et le menu, le trait dans l'Overlay"

# ------------------------------------------------------------------------ 2. le noyau, 3. les gestes

# $1 projet · $2 tests attendus au moins · $3 ce qu'ils éprouvent
jouer() {
    if ! dotnet test "$1" ${MYPUBLISHER_DEJA_COMPILE:+--no-build} --nologo --filter "poste=N25" > "$travail/tests.txt" 2>&1; then
        grep -E "Failed |Error Message|Assert" -A3 "$travail/tests.txt" | sed 's/^/    /' >&2 || sed 's/^/    /' "$travail/tests.txt" >&2
        echo "ÉCHEC : la vérification orthographique (« poste=N25 », $1) ne passe pas." >&2
        exit 1
    fi
    joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
    if [ "${joues:-0}" -lt "$2" ]; then
        echo "ÉCHEC : $2 tests attendus sous « poste=N25 » dans $1, ${joues:-0} joués." >&2
        echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
        exit 1
    fi
}

jouer MyPublisher.Tests/MyPublisher.Tests.csproj 10
echo "  2. le noyau : $joues tests (la langue et son fichier, les mots, « recieve » et « receive », le cache, le dictionnaire absent, la langue de chaque mot, Ignorer tout et Ajouter, les préférences)"

jouer MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj 10
echo "  3. les gestes sur la vraie fenêtre : $joues tests (« recieve » souligné aux places de N3 ; l'encre et le PDF inchangés ; F7 dans l'ordre de lecture ; Remplacer en un pas ; le dictionnaire de l'utilisateur après redémarrage ; le clic droit ; le mot qu'on tape et la case ; le bloc tourné ; la langue de chaque mot et le dictionnaire absent ; sans correcteur)"

# ------------------------------------------------------------------------ 4. le relevé d'ATA 21

dictionnaire=/usr/share/hunspell/en_US.dic
if [ ! -f "$dictionnaire" ] || [ ! -f "${dictionnaire%.dic}.aff" ]; then
    echo "  4. SAUTÉ : $dictionnaire est absent (sudo apt install hunspell-en-us) — le relevé d'ATA 21 en en-US ne se fait pas, et rien n'en est conclu."
else
    outil="${MYPUBLISHER_PUB2RAW:-$(Scripts/outils-pub.sh --chemin 2>/dev/null || true)}"
    if [ -z "$outil" ] || [ ! -x "$outil" ]; then
        echo "ÉCHEC : « pub2raw » est introuvable : sudo apt install libmspub-tools, ou Scripts/outils-pub.sh" >&2
        exit 1
    fi
    export MYPUBLISHER_PUB2RAW="$outil"
    if [ -d "$racine/.outils/mspub/lib" ]; then
        export LD_LIBRARY_PATH="$racine/.outils/mspub/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
    fi

    banc="MyPublisher.Banc/bin/Debug/net10.0/MyPublisher.Banc"
    if ! "$banc" orthographe "MyPublisher/Books/ATA 21 - Air conditioning.pub" --langue en-US > "$travail/ata21.txt" 2>&1; then
        sed 's/^/    /' "$travail/ata21.txt" >&2
        echo "ÉCHEC : le relevé d'ATA 21 en en-US n'est pas stable, ou n'a pas pu se faire." >&2
        exit 1
    fi
    grep -q "^stable : oui$" "$travail/ata21.txt" || { sed 's/^/    /' "$travail/ata21.txt" >&2; echo "ÉCHEC : le relevé ne se dit pas stable." >&2; exit 1; }
    fautes="$(sed -n 's/^fautes : //p' "$travail/ata21.txt")"
    mots="$(sed -n 's/^mots : //p' "$travail/ata21.txt")"
    echo "  4. ATA 21 en en-US, sur $dictionnaire : $fautes mots signalés sur $mots, les mêmes trois fois (correcteur neuf, cache plein, second correcteur)"
    sed -n 's/^premieres : /       — /p' "$travail/ata21.txt"
fi

echo "N25 : les fautes se soulignent sur la page sans s'imprimer, F7 les parcourt dans la barre de recherche, le clic droit les corrige, et le dictionnaire de l'utilisateur survit au redémarrage."
