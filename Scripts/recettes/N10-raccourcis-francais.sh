#!/usr/bin/env bash
#
# Recette du poste N10 — les raccourcis de Publisher en français (fiche 23, b2, b7, b8, b10, d8,
# d20 ; fiche 24).
#
# Avant le poste, Ctrl+E exportait en PDF là où Publisher centre, même pendant une saisie, et
# Ctrl+Maj+H dissociait là où Publisher dissocie par Ctrl+Maj+G. La table de l'aide de Microsoft
# en français est devenue la fiche 24, et la règle 12 de la phase N y fait son sort à chaque
# touche. Le poste lie les alignements, le corps, l'export sur Ctrl+Maj+P, Ctrl+Maj+G qui groupe
# et dissocie, Alt+Retour arrière et F4, Ctrl+Maj+X. Les raccourcis qui sont des caractères se
# lisent sur le caractère, comme la sonde du clavier l'a relevé sur l'AZERTY du propriétaire.
# Deux temps.
#
# 1. LES SOURCES, lues par un script :
#
#      a. la fiche 24 existe, avec l'adresse de l'aide et sa date de consultation, et recopie mot
#         pour mot les lignes des touches que le poste lie ; l'index la range ;
#      b. la table des raccourcis par caractère — « ] », « [ », « > », « < » — est dans le
#         partiel du poste (règle 10), et la fenêtre la lit sur le caractère produit, avant la
#         garde de la saisie, Maj et Alt comptés pour rien ;
#      c. la liste standard des corps est dans le noyau, de 8 à 72 pt ;
#      d. les liaisons : Ctrl+L, E, R, J alignent ; Ctrl+Maj+P exporte, par une commande du
#         modèle de vue que la fenêtre suit, et Ctrl+E n'exporte plus ; Ctrl+Maj+G groupe ou
#         dissocie, Ctrl+Maj+H est libre ; Ctrl+Maj+X supprime ; Alt+Retour arrière et F4
#         annulent et rétablissent ; les menus et les infobulles disent les touches ;
#      e. la sonde du clavier ne s'éveille que par sa variable ; Rendu.ToucheAzerty rejoue ses
#         relevés ;
#      f. le README dit les touches, à leur sous-tableau.
#
# 2. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre, au clavier
#    (« RaccourcisFrancaisTests », trait « poste=N10 ») — les clauses de la recette du plan :
#
#      · Ctrl+E centre le texte en un pas et n'exporte plus, ni sur une forme, ni sans
#        sélection ; Ctrl+Maj+P exporte, même en saisie, que la frappe garde ;
#      · Ctrl+L, E, R, J alignent, hors saisie et en saisie, un pas chacun ;
#      · Ctrl+] et Ctrl+[ changent le corps d'un point, Ctrl+Maj+> et Ctrl+Maj+< de taille dans
#        la liste standard — aux frappes AZERTY de la sonde, et en QWERTY —, en un pas, hors
#        saisie et en saisie ; un tableau aussi, une forme non ;
#      · Ctrl+Maj+G groupe, puis dissocie ; Ctrl+Maj+H ne fait plus rien ;
#      · Alt+Retour arrière annule, F4 rétablit, hors saisie et en saisie ; sans rien à annuler,
#        Alt+Retour arrière ne supprime pas le bloc ;
#      · Ctrl+Maj+X supprime le bloc, hors saisie et en saisie, la frappe gardant son pas ;
#      · Ctrl + « à » donne la taille réelle, sans Maj ; Ctrl + « = » et Ctrl + « ) » zooment,
#        comme la sonde les a vus pris.
#
#    Puis la liste standard dans le noyau (« CorpsStandardTests », même trait), et les voisins
#    que la règle 14 a réécrits : la marge où Ctrl+Maj+G ne groupe ni ne dissocie (N6), la barre
#    dont l'infobulle dit Ctrl+Maj+G (I3), le menu Texte qui montre ses touches (I2). La recette
#    de A4, étendue par le poste, se joue à part.
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
    chemin = racine / chemin
    return chemin.read_text(encoding="utf-8") if chemin.exists() else ""

def corps(source, signature):
    """Le corps d'une méthode, de son accolade ouvrante à la fermante de même retrait."""
    trouve = re.search(re.escape(signature) + r"\s*\n(\s*)\{\n(.*?)\n\1\}", source, re.S)
    return trouve.group(2) if trouve else None

# ------------------------------------------------------------------- a. la fiche 24

fiche = lire("MyPublisher/Docs/Publisher2007/24-raccourcis-clavier-francais.md")
exige(fiche, "la fiche 24 n'existe pas (Docs/Publisher2007/24-raccourcis-clavier-francais.md).")
exige("https://support.microsoft.com/fr-fr/publisher/keyboard-shortcuts-in-publisher" in fiche,
      "la fiche 24 ne donne pas l'adresse de l'aide de Microsoft.")
exige(re.search(r"consultée le\s+\d+\s+\w+\s+20\d\d", fiche), "la fiche 24 ne date pas sa consultation.")
# Les lignes de l'aide, mot pour mot, pour chaque touche que le poste lie.
for fonction, touche in (("Centrer un paragraphe", "CTRL+E"), ("Aligner un paragraphe à gauche", "Ctrl+L"),
                         ("Aligner un paragraphe à droite", "Ctrl+R"),
                         ("Aligner un paragraphe sur les deux côtés (justifié)", "Ctrl+J"),
                         ("Augmenter la taille de police de 1.0 point", "Ctrl+]"),
                         ("Réduire la taille de police de 1.0 point", "Ctrl+["),
                         ("Augmenter à la taille suivante dans la zone Taille de police", "CTRL+MAJ+>"),
                         ("Diminuer à la taille suivante dans la zone Taille de police", "CTRL+MAJ+<"),
                         ("Grouper les objets sélectionnés ou dissocier les objets groupés", "Ctrl+Maj+G"),
                         ("Supprimer l’objet sélectionné", "Supprimer ou Ctrl+Maj+X"),
                         ("Annuler votre dernière action", "Ctrl+Z ou Alt+Ret. arr"),
                         ("Répéter votre dernière action", "Ctrl+Y ou F4"),
                         ("Afficher la boîte de dialogue Coupure de mots", "Ctrl+Maj+H"),
                         ("Ouvrir la boîte de dialogue Police", "Ctrl+Maj+F")):
    exige(f"| {fonction} | {touche} |" in fiche, f"la fiche 24 ne recopie pas la ligne « {fonction} » ({touche}).")
exige("[24](24-raccourcis-clavier-francais.md)" in lire("MyPublisher/Docs/Publisher2007/00-index.md"),
      "l'index des fiches ne range pas la fiche 24.")
print("    a. la fiche 24 : l'adresse, la date, les lignes de l'aide mot pour mot ; l'index")

# ------------------------------------------------- b. la table des raccourcis par caractère

partiel = lire("MyPublisher/ViewModels/MainWindowViewModel.Raccourcis.cs")
modele = lire("MyPublisher/ViewModels/MainWindowViewModel.cs")
fenetre = lire("MyPublisher/Views/MainWindow.axaml.cs")
exige("public partial class MainWindowViewModel" in partiel,
      "ce que N10 ajoute au modèle de vue n'est pas dans son partiel, MainWindowViewModel.Raccourcis.cs (règle 10).")
table = re.search(r"RaccourcisParCaractere => .*?\{(.*?)\};", partiel, re.S)
exige(table is not None, "la table « RaccourcisParCaractere » n'est pas dans le partiel du poste.")
if table:
    attendue = {"]": "CorpsPlusUnPoint", "[": "CorpsMoinsUnPoint", ">": "CorpsSuivant", "<": "CorpsPrecedent"}
    lue = dict(re.findall(r'\["(.)"\] = (\w+)Command', table.group(1)))
    exige(lue == attendue, f"la table des raccourcis par caractère est {lue}, {attendue} attendue.")
for membre in ("RaccourcisParCaractere", "JouerLeRaccourciParCaractere", "ExportDemande", "ToucheGrouperOuDissocier",
               "ToucheSupprimerLObjet"):
    exige(membre not in modele, f"« {membre} » est dans MainWindowViewModel.cs, hors du partiel du poste.")
cle = corps(fenetre, "protected override void OnKeyDown(KeyEventArgs e)") or ""
lecture = cle.find("vm.JouerLeRaccourciParCaractere(e.KeySymbol)")
garde = cle.find("if (PageView.IsEditingText || ConsumesTyping(")
exige(lecture >= 0, "OnKeyDown ne lit pas la table sur le caractère produit (e.KeySymbol).")
exige(0 <= lecture < garde, "la table est lue après la garde de la saisie : en saisie, le corps ne changerait pas.")
exige("(e.KeyModifiers & ~(KeyModifiers.Shift | KeyModifiers.Alt)) == KeyModifiers.Control" in fenetre,
      "la table ne compte pas pour rien Maj et Alt : sur un AZERTY, Ctrl+Maj+« < » ne donnerait pas « > ».")
print("    b. la table des raccourcis par caractère, au partiel du poste, lue sur le caractère avant la garde de la saisie")

# ------------------------------------------------------------- c. la liste standard

noyau = lire("MyPublisher.Core/Models/CorpsStandard.cs")
exige("[8, 9, 10, 10.5, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72]" in noyau,
      "la liste standard des corps, de 8 à 72 pt, n'est pas dans le noyau (CorpsStandard).")
exige("Avalonia" not in re.sub(r"///.*|//.*", "", noyau), "CorpsStandard dépend d'Avalonia : le noyau n'en a pas.")
print("    c. la liste standard des corps, dans le noyau")

# ------------------------------------------------------------------- d. les liaisons

axaml = lire("MyPublisher/Views/MainWindow.axaml")
liaisons = re.findall(r'<KeyBinding\s+Gesture="([^"]+)"\s+Command="\{Binding (\w+)\}"(?:\s+CommandParameter="([^"]*)")?', axaml)
def lie(geste, commande, parametre=""):
    return (geste, commande, parametre) in liaisons
for geste, alignement in (("Ctrl+L", "Left"), ("Ctrl+E", "Center"), ("Ctrl+R", "Right"), ("Ctrl+J", "Justify")):
    exige(lie(geste, "AlignerLeTexteCommand", "{x:Static models:TextAlign." + alignement + "}"),
          f"« {geste} » n'aligne pas le texte ({alignement}).")
exige(lie("Ctrl+Shift+P", "ExporterEnPdfCommand"), "« Ctrl+Shift+P » n'exporte pas.")
exige([g for g, c, _ in liaisons if c == "ExporterEnPdfCommand"] == ["Ctrl+Shift+P"], "l'export a une autre touche que Ctrl+Maj+P.")
exige(not re.search(r"case Key\.E: On", fenetre), "OnKeyDown exporte encore à Ctrl+E.")
exige("_vm.ExportDemande += OnExportDemande;" in fenetre and "_vm.ExportDemande -= OnExportDemande;" in fenetre,
      "la fenêtre ne suit pas la demande d'export du modèle de vue, ou ne la lâche pas.")
exige(lie("Ctrl+Shift+G", "ToucheGrouperOuDissocierCommand"), "« Ctrl+Shift+G » ne groupe ni ne dissocie.")
exige(not any(g == "Ctrl+Shift+H" for g, _, _ in liaisons), "« Ctrl+Shift+H » est encore lié : il est réservé à la coupure de mots.")
exige(lie("Ctrl+Shift+X", "ToucheSupprimerLObjetCommand"), "« Ctrl+Shift+X » ne supprime pas l'objet.")
exige(lie("Alt+Back", "UndoCommand") and lie("F4", "RedoCommand"), "Alt+Retour arrière et F4 n'annulent ni ne rétablissent.")
exige('Header="Exporter en _PDF…" InputGesture="Ctrl+Shift+P" Command="{Binding ExporterEnPdfCommand}"' in axaml,
      "le menu Fichier ne montre pas Ctrl+Maj+P sur l'export.")
exige('ToolTip.Tip="Exporter en PDF (Ctrl+Maj+P)"' in axaml, "le bouton d'export dit encore une autre touche.")
exige('<MenuItem Header="_Dissocier" InputGesture="Ctrl+Shift+G"' in axaml, "le menu Disposition ne montre pas Ctrl+Maj+G à Dissocier.")
exige('ToolTip.Tip="Dissocier (Ctrl+Maj+G)"' in lire("MyPublisher/Views/BarreContextuelle.axaml"),
      "l'infobulle de la barre dit encore Ctrl+Maj+H.")
exige("Ctrl+Maj+H" not in re.sub(r"<!--.*?-->", "", axaml, flags=re.S), "la fenêtre affiche encore Ctrl+Maj+H.")
print("    d. les liaisons, les menus et les infobulles : alignements, export, grouper, supprimer, annuler, rétablir")

# ----------------------------------------------------------------------- e. la sonde

sonde = lire("MyPublisher/Views/SondeDuClavier.cs")
exige('public const string Variable = "MYPUBLISHER_SONDE_CLAVIER";' in sonde, "la sonde du clavier n'a plus sa variable.")
exige("SondeDuClavier.BrancherSiDemandee();" in lire("MyPublisher/App.axaml.cs"), "l'application ne branche plus la sonde.")
rendu = lire("MyPublisher.Tests.Interface/Rendu.cs")
for releve in ('[(PhysicalKey.IntlBackslash, NiveauAzerty.Base)] = (Key.OemComma, "<")',
               '[(PhysicalKey.IntlBackslash, NiveauAzerty.Maj)] = (Key.OemPeriod, ">")',
               '[(PhysicalKey.Digit5, NiveauAzerty.AltGr)] = (Key.D5, "[")',
               '[(PhysicalKey.Digit0, NiveauAzerty.Base)] = (Key.D0, "à")'):
    exige(releve in rendu, f"Rendu.ToucheAzerty ne rejoue plus le relevé {releve}.")
print("    e. la sonde, par sa variable ; ses relevés, rejoués par Rendu.ToucheAzerty")

# ------------------------------------------------------------------------ f. le README

readme = lire("README.md")
tableau = re.search(r"^## Raccourcis$(.*?)^## ", readme, re.S | re.M)
exige(tableau is not None, "le README n'a plus de section « ## Raccourcis ».")
if tableau:
    for ligne in ("| `Ctrl+Maj+P` | Exporter en PDF", "| `Ctrl+L` / `Ctrl+E` / `Ctrl+R` / `Ctrl+J` |",
                  "| `Ctrl+]` / `Ctrl+[` |", "| `Ctrl+Maj+>` / `Ctrl+Maj+<` |", "| `Ctrl+Maj+G` | Grouper",
                  "| `Ctrl+Maj+X` |", "`Alt+Retour arrière`", "`F4`", "24-raccourcis-clavier-francais.md"):
        exige(ligne in tableau.group(1), f"la section des raccourcis du README ne dit pas « {ligne} ».")
    exige("`Ctrl+E` | Exporter" not in tableau.group(1), "le README dit encore que Ctrl+E exporte.")
print("    f. le README dit les touches, à leur sous-tableau")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : la fiche 24, la table par caractère, la liste standard, les liaisons et les menus, la sonde, le README"

# ------------------------------------------------------------------------ 2. les gestes

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

interface=MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj
jouer "$interface" "poste=N10" 9 "2. les raccourcis français, sur la vraie fenêtre : Ctrl+E qui centre, l'export sur Ctrl+Maj+P, les alignements, le corps aux frappes de la sonde, grouper et dissocier, supprimer, annuler et rétablir, Ctrl + « à »"
jouer MyPublisher.Tests/MyPublisher.Tests.csproj "poste=N10" 19 "   la liste standard des corps, dans le noyau"

# Les voisins que la règle 14 a réécrits : la touche qui dissociait, et les menus qui la montraient.
voisins="FullyQualifiedName~ToucheAuChampTests.Dans_un_champ_grouper_et_dissocier_se_taisent"
voisins+="|FullyQualifiedName~BarreContextuelleTests.Passer_d_un_texte_a_une_image_change_la_barre_sans_deplacer_la_planche"
voisins+="|FullyQualifiedName~DispositionTests.Le_menu_Texte_porte_le_gras_l_italique_les_alignements_et_l_import"
jouer "$interface" "$voisins" 3 "   les voisins réécrits : Ctrl+Maj+G muet dans un champ (N6), l'infobulle de la barre (I3), le menu Texte (I2)"

echo "N10 : les raccourcis de Publisher en français — Ctrl+E centre, Ctrl+Maj+P exporte, le corps se lit sur le caractère, Ctrl+Maj+G groupe et dissocie."
