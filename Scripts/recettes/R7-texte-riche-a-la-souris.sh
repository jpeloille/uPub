#!/usr/bin/env bash
#
# Recette du poste R7 — éditer le texte riche à la souris (spécifié le 11 octobre 2026, repris
# dans la phase N, salve Saisie 8, en voie 2a ; fiche 23, b1, b4, b13, b18, b19).
#
# Les fragments de style se lisaient, s'écrivaient et se rendaient, mais la barre, le menu Texte
# et Ctrl+G n'agissaient que sur le bloc entier : on ne pouvait pas mettre un mot en gras. En
# saisie, la police, le corps, le gras, l'italique, le souligné et ses sortes, la couleur,
# Ctrl+Espace, Ctrl+Maj+C et Ctrl+Maj+V visent désormais la sélection du texte — ou, sans
# sélection, le mot du curseur — et l'écrivent en fragments par MiseEnForme (N2) ; la barre montre
# ce que les caractères visés ont en commun, vide quand c'est mixte. Hors saisie, le bloc veut dire
# tous ses caractères, fragments compris. Deux temps.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. le modèle de vue : ce que R7 ajoute est dans son partiel (règle 10) — la
#         MiseEnFormeVisee, qui lit par MiseEnForme.Lire et écrit par MiseEnForme.Poser, Basculer et
#         Retirer ; le gras, l'italique, le souligné, ses sortes et le corps passent par elle pour
#         un bloc de texte ;
#      b. la barre : la police, le corps, G, I et S se lient à elle, sous les noms du bloc et dans un
#         seul sens pour les boutons — les comptes de la recette I3 tiennent —, la police sous la
#         garde JamaisNul de Q20 ; la couleur aussi ; les sortes du souligné cochent la sienne ;
#         l'interligne reste au bloc ;
#      c. l'apparence dessine le bouton à moitié allumé (IsChecked nul) : la teinte de la
#         projection ;
#      d. les touches de la fiche 24 : Ctrl+Espace, Ctrl+Maj+C et Ctrl+Maj+V liés une fois, au menu
#         Texte, au README et liés dans la fiche ; Ctrl+G dit sa portée au README ;
#      e. le piège de Q14 : le champ du corps qui ne montre rien — l'état mixte — vide la mémoire
#         de sa case.
#
# 2. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre (trait
#    « poste=R7 » : TexteRicheALaSourisTests) — la recette du plan, et ce qui l'entoure :
#
#      · les caractères 4 à 7 sélectionnés au pointeur, Ctrl+G : un seul fragment
#        {Start 4, Length 3, Bold}, la frappe continue, et le PDF dessine ces trois caractères
#        seuls dans la fonte grasse (relevé du flux) ; un pas ;
#      · Ctrl+G sur une sélection grasse retire le gras et le fragment ; sur une sélection mixte,
#        il met le gras partout, le bouton G à moitié allumé avant ; un pas par commande ;
#      · le corps 14 posé à la barre va à la sélection, en un pas — et une taille de la liste ;
#      · sans sélection, le mot du curseur ; au bord d'un mot, rien, et la barre d'état le dit ;
#      · hors saisie, tous les caractères du bloc : le gras d'un fragment importé s'atteint, sur
#        une feuille et sur les blocs d'ATA 21 ; le corps de chacun depuis le sien ;
#      · la barre montre l'état de la sélection, vide quand il est mixte, sans rien écrire ; la
#        police, la couleur, le souligné et ses sortes, Ctrl+Espace, Ctrl+Maj+C et Ctrl+Maj+V, les
#        touches du corps ; la mémoire du corps face à une valeur nulle.
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
import pathlib, re, sys, xml.etree.ElementTree as ET

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
    """Le code sans ses commentaires de documentation."""
    return re.sub(r"^\s*///.*$", "", texte, flags=re.M)

# a. le modèle de vue.
partiel = code(lire("MyPublisher/ViewModels/MainWindowViewModel.MiseEnFormeVisee.cs"))
exige("public sealed class MiseEnFormeVisee" in partiel and "public partial class MainWindowViewModel" in partiel,
      "la MiseEnFormeVisee n'est pas dans le partiel de R7 (règle 10).")
for appel in ("MiseEnForme.Lire(", "MiseEnForme.Allume(", "MiseEnForme.Poser(", "MiseEnForme.Basculer(", "MiseEnForme.Retirer("):
    exige(appel in partiel, f"la MiseEnFormeVisee n'appelle pas {appel}…) : elle doit lire et écrire par la mise en forme du noyau (N2).")
exige(re.search(r"_vm\.SelectionDuTexte is \{ Case: null \} selection", partiel) is not None,
      "la MiseEnFormeVisee ne lit pas la sélection du texte (N15).")
exige("MotDuCurseur(texte, selection.Curseur)" in partiel, "sans sélection, la MiseEnFormeVisee ne vise pas le mot du curseur.")
for fichier, motif, quoi in (
        ("MainWindowViewModel.Disposition.cs", r"BasculerLeTexte\(Bascule\.Gras, \"Gras\"\)", "le gras"),
        ("MainWindowViewModel.Disposition.cs", r"BasculerLeTexte\(Bascule\.Italique, \"Italique\"\)", "l'italique"),
        ("MainWindowViewModel.Souligne.cs", r"BasculerLeTexte\(Bascule\.Souligne, \"Souligné\"\)", "le souligné"),
        ("MainWindowViewModel.BarreContextuelle.cs", r"visee\.Poser\(new StyleDeLaPlage \{ Underline = souligne \}\)", "les sortes du souligné"),
        ("MainWindowViewModel.Raccourcis.cs", r"visee\.ChangerLeCorps\(nouveau\)", "le corps")):
    exige(re.search(motif, code(lire("MyPublisher/ViewModels/" + fichier))) is not None,
          f"{quoi} d'un bloc de texte ne passe pas par la MiseEnFormeVisee ({fichier}).")
print("    a. le partiel de R7 : la MiseEnFormeVisee lit et écrit par MiseEnForme ; gras, italique, souligné, sortes et corps passent par elle")

# b. la barre.
barre = ET.fromstring(lire("MyPublisher/Views/BarreContextuelle.axaml"))
gabarit = next((g for g in barre.iter(A + "DataTemplate") if g.get("DataType") == "models:TextFrame"), None)
exige(gabarit is not None, "la barre n'a plus de gabarit pour le bloc de texte.")
if gabarit is not None:
    groupes = [e for e in gabarit.iter() if e.get(X + "Name") == "MiseEnFormeDuTexte"]
    exige(len(groupes) == 1, "la mise en forme du texte n'est pas rangée sous « MiseEnFormeDuTexte ».")
    if groupes:
        groupe = groupes[0]
        exige(groupe.get(X + "DataType") == "vm:MiseEnFormeVisee" and "MiseEnFormeVisee}" in (groupe.get("DataContext") or ""),
              "la police, le corps, G, I et S ne se lient pas à la MiseEnFormeVisee.")
        attendus = {
            "PoliceDuBloc": ("SelectedItem", "{Binding FontFamily, Converter={x:Static conv:JamaisNul.Instance}}"),
            "CorpsDuBloc": ("Value", "{Binding FontSize, Converter={x:Static conv:DoubleToDecimal.Instance}}"),
        }
        for nom, (propriete, liaison) in attendus.items():
            controles = [e for e in groupe.iter() if e.get(X + "Name") == nom]
            exige(len(controles) == 1 and controles[0].get(propriete) == liaison,
                  f"« {nom} » n'est pas dans le groupe, lié par {liaison} (la garde de Q20 pour la police).")
        boutons = {b.get("ToolTip.Tip"): b.get("IsChecked") for b in groupe.iter(VIEWS + "BoutonDEtat")}
        exige(boutons == {"Gras (Ctrl+G)": "{Binding Bold, Mode=OneWay}", "Italique (Ctrl+I)": "{Binding Italic, Mode=OneWay}",
                          "Souligné (Ctrl+U)": "{Binding Underline.Actif, Mode=OneWay}"},
              f"G, I et S ne se cochent pas par la MiseEnFormeVisee, dans un seul sens : {boutons}.")
        sortes = [m for m in groupe.iter(A + "MenuItem")]
        exige(len(sortes) == 4 and all("MiseEnFormeVisee.Underline.Sorte" in (m.get("IsChecked") or "") for m in sortes),
              "les sortes du souligné ne cochent pas celle des caractères visés.")
        exige(not any(e.get(X + "Name") == "InterligneDuBloc" for e in groupe.iter()), "l'interligne est passé aux caractères : il reste au bloc.")
    couleurs = [e for e in gabarit.iter() if e.get(X + "Name") == "CouleurDuTexte"]
    exige(len(couleurs) == 1 and couleurs[0].get(X + "DataType") == "vm:MiseEnFormeVisee"
          and "MiseEnFormeVisee}" in (couleurs[0].get("DataContext") or ""),
          "la couleur du texte ne se lie pas à la MiseEnFormeVisee.")
    interlignes = [e for e in gabarit.iter() if e.get(X + "Name") == "InterligneDuBloc"]
    exige(len(interlignes) == 1 and interlignes[0].get("Value", "").startswith("{Binding LineHeight"), "l'interligne n'est plus lié au bloc.")
print("    b. la barre : police (sous JamaisNul), corps, G, I, S, sortes et couleur sur les caractères visés ; l'interligne au bloc")

# c. l'apparence.
apparence = lire("MyPublisher/Views/Apparence.axaml")
exige(re.search(r'<Style Selector="ToggleButton\.ib:indeterminate /template/ ContentPresenter#PART_ContentPresenter">\s*'
                r'<Setter Property="Background" Value="\{StaticResource Projection\}"/>', apparence) is not None,
      "le bouton à moitié allumé n'a pas son style (la teinte de la projection).")
print("    c. l'apparence : le bouton à moitié allumé, sur la teinte de la projection")

# d. les touches.
fenetre = ET.fromstring(lire("MyPublisher/Views/MainWindow.axaml"))
liaisons = [(e.get("Gesture"), e.get("Command")) for e in fenetre.iter(A + "KeyBinding")]
touches = {"Ctrl+Space": "RetirerLaMiseEnFormeCommand", "Ctrl+Shift+C": "CopierLaMiseEnFormeCommand",
           "Ctrl+Shift+V": "CollerLaMiseEnFormeCommand"}
readme = lire("README.md")
fiche = lire("MyPublisher/Docs/Publisher2007/24-raccourcis-clavier-francais.md")
for geste, commande in touches.items():
    exige([c for g, c in liaisons if g == geste] == ["{Binding " + commande + "}"], f"« {geste} » n'est pas lié une fois, sur {commande}.")
    menus = [m for m in fenetre.iter(A + "MenuItem") if m.get("Command") == "{Binding " + commande + "}"]
    exige(len(menus) == 1 and menus[0].get("InputGesture") == geste, f"le menu Texte n'offre pas {commande} sous « {geste} ».")
    au_readme = geste.replace("Shift", "Maj").replace("Space", "Espace")
    exige(re.search(r"^\| .*`" + re.escape(au_readme) + r"`.* \|$", readme, re.M) is not None, f"« {au_readme} » n'est pas au tableau du README.")
for fonction in ("Copier la mise en forme | Ctrl+Maj+C", "Coller la mise en forme | Ctrl+Maj+V",
                 "le style de texte actuel | Ctrl+Espace"):
    exige(re.search(re.escape(fonction) + r" \| HR \| \*\*lié\*\* \(R7\)", fiche) is not None,
          f"la fiche 24 ne dit pas « {fonction} » lié par R7.")
exige(re.search(r"^\| `Ctrl\+G` / `Ctrl\+I` / `Ctrl\+U` \| .*en saisie, la sélection du texte, ou le mot du curseur ; hors saisie, tous les caractères du bloc",
                readme, re.M) is not None,
      "le README ne dit pas la portée de Ctrl+G en saisie et hors saisie.")
exige("ne se saisissent pas\n  encore à la souris" not in readme, "le README range encore le texte riche à la souris parmi les limites.")
print("    d. Ctrl+Espace, Ctrl+Maj+C, Ctrl+Maj+V : liés, au menu Texte, au README et à la fiche 24 ; la portée de Ctrl+G")

# e. le piège de Q14.
convertisseurs = code(lire("MyPublisher/Converters/UnitConverters.cs"))
exige(re.search(r"_affiches\.Retenir\(\(decimal\)Math\.Round\(number, 4\), number\) : _affiches\.Oublier\(\);", convertisseurs) is not None,
      "le champ du corps qui ne montre rien garde la mémoire de sa case (Q14).")
print("    e. le champ du corps vide sa mémoire quand il ne montre rien (Q14)")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : le partiel, la barre, l'apparence, les touches, la mémoire de Q14"

# ------------------------------------------------------------------------ 2. les gestes

attendus=24
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo --filter "poste=R7" > "$travail/tests.txt" 2>&1; then
    grep -E "Failed |Error Message|Assert" -A3 "$travail/tests.txt" | sed 's/^/    /' >&2 || sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : le texte riche à la souris (« poste=R7 ») ne passe pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=R7 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  2. les gestes sur la vraie fenêtre : $joues tests (« def » sélectionné au pointeur, Ctrl+G, un fragment, gras seul au PDF, un pas ; le gras retiré ; la sélection mixte ; le corps 14 à la barre ; le mot du curseur ; le bloc entier hors saisie, ATA 21 compris ; l'état de la barre ; police, couleur, souligné ; Ctrl+Espace, Ctrl+Maj+C et V ; le corps de chacun ; la mémoire de Q14)"

echo "R7 : en saisie, la mise en forme va à la sélection du texte, ou au mot du curseur ; hors saisie, à tous les caractères du bloc."
