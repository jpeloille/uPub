#!/usr/bin/env bash
#
# Recette des postes Q24 et Q25 — qui tient la transaction d'édition, et quand.
#
# Une transaction s'ouvre pour un geste et se referme avec lui ; elle n'empile son pas
# d'annulation qu'à sa fermeture. Ses propriétaires : la saisie sur la page, le champ du dock
# ou de la barre qui a le focus, le glissé sur la planche, le sélecteur de couleur de la barre.
#
#   Q24 — Annuler et Rétablir referment tout ce qui est ouvert, puis lèvent un avis : chaque
#         propriétaire encore actif rouvre sa propre transaction, et lui seul. Avant, Ctrl+Z
#         sans rien à annuler, ou Ctrl+Y après n'importe quelle frappe, laissaient la saisie —
#         ou le champ — hors de toute transaction : ce qu'on tapait ensuite ne s'annulait
#         jamais, et ne marquait pas le document.
#
#   Q25 — La transaction de la saisie suit le focus de sa zone sur la page : ouverte quand
#         elle le prend, refermée quand elle le perd, et à la fin de la saisie. Avant, elle
#         courait du double-clic à Échap, et un champ de la barre ou du dock qui prenait le
#         focus pendant la frappe y emboîtait la sienne : le corps changé entre deux mots se
#         fondait dans le pas de la frappe (règle 2). Le sélecteur de couleur referme la
#         sienne avant de rendre le focus à la zone.
#
# Trois temps :
#
#   1. LES SOURCES, lues par un script :
#      a. Undo et Redo referment d'abord ce qui est ouvert, lèvent l'avis sur chaque chemin —
#         rien à défaire compris : aucun retour avant lui —, et ne rouvrent rien eux-mêmes :
#         aucun BeginEdit. L'avis est déclaré dans le partiel du poste (règle 10), et n'est
#         levé que par eux ;
#      b. ses trois auditeurs, la fenêtre (les champs), la planche (la saisie, le glissé) et
#         la barre (le sélecteur de couleur), s'y abonnent et s'en désabonnent une fois
#         chacun ;
#      c. (Q25) BeginTextEdit n'ouvre plus de transaction, EndTextEdit ne referme que celle
#         que la saisie tient ; la planche suit le focus de la zone (GotFocus, LostFocus) ;
#         la barre referme la transaction de la couleur dès que le volet se referme
#         (Closing), avant qu'il ne rende le focus ;
#   2. LES TESTS d'interface « poste=Q24 », sur la vraie fenêtre, au clavier : Ctrl+Z sans
#      rien à annuler pendant la saisie, puis une frappe — un pas, le document modifié, le
#      titre marqué, et Ctrl+Z rend le texte d'avant ; une frappe, Ctrl+Y, une frappe — la
#      saisie reste ouverte, deux pas ; un champ de la barre qui garde le focus pendant un
#      Ctrl+Z sans rien à annuler ; le champ Contenu sous Ctrl+Y ; une marge de l'onglet
#      Document, que l'annulation laisse au focus ; un glissé repris après un Ctrl+Z tenu ; le
#      sélecteur de couleur resté ouvert sous un Ctrl+Z ; et le piège du poste — après une annulation qui referme la saisie, un glissé de 1 pt, Alt
#      tenu, fait encore son propre pas ; enfin la règle de Q14 vue de la transaction —
#      entrer en saisie et la quitter sans rien taper ne fait aucun pas. Puis les tests
#      voisins qui tiennent « un geste, un pas » (I2, I3) : la règle d'un pas par champ
#      reste intacte.
#
#      Le test du double-clic de Q14 sur ATA 21 n'est pas de ces voisins : joué en premier
#      dans un processus neuf, son premier clic — le premier de la fenêtre, à froid —
#      dépasse parfois les 500 ms du double-clic sous la charge de la machine, sur le code
#      d'avant Q24 comme après (mesuré le 26 octobre 2026). Les recettes de Q14 et de Q23
#      le jouent, à chaud.
#   3. LES TESTS d'interface « poste=Q25 » : pendant une saisie sur la page, une frappe, un
#      réglage, un clic dans le texte, une frappe — trois pas, défaits un à un et dans
#      l'ordre, pour le corps à la barre, un retrait au dock et la couleur prise au
#      sélecteur ; et le gras cliqué à la barre, qui ne prend pas le focus, fait encore le
#      sien par UnGesteUnPas ; enfin, un Ctrl+Z sans rien à annuler pendant la saisie,
#      le focus dans un champ de la barre : seul le champ rouvre sa transaction (Q24 et Q25).
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

# ------------------------------------------------- a. Annuler et Rétablir lèvent l'avis

modele = (VM / "MainWindowViewModel.cs").read_text(encoding="utf-8")
partiel = VM / "MainWindowViewModel.Transactions.cs"
exige(partiel.exists(), "l'avis n'est pas dans le partiel du poste, MainWindowViewModel.Transactions.cs (règle 10).")
if partiel.exists():
    texte = partiel.read_text(encoding="utf-8")
    exige("public event EventHandler? TransactionsRefermees;" in texte, "le partiel ne déclare pas l'avis « TransactionsRefermees ».")
    exige(re.search(r"void AviserLesProprietaires\(\) => TransactionsRefermees\?\.Invoke\(this, EventArgs\.Empty\);", texte),
          "« AviserLesProprietaires » ne lève pas l'avis.")
    exige("TransactionsRefermees" not in modele, "l'avis est déclaré ou levé hors du partiel du poste.")

for methode in ("Undo", "Redo"):
    c = corps(modele, f"private void {methode}()")
    exige(c is not None, f"« {methode} » est introuvable dans MainWindowViewModel.cs.")
    if c is None:
        continue
    instructions = [l.strip() for l in c.splitlines() if l.strip() and not l.strip().startswith("//")]
    exige(instructions[:1] == ["EndPendingEdit();"], f"« {methode} » ne referme plus d'abord ce qui est ouvert.")
    exige(instructions[-1:] == ["AviserLesProprietaires();"],
          f"« {methode} » ne lève pas l'avis en dernier, hors de toute condition.")
    exige(not re.search(r"\breturn\b", c), f"« {methode} » a un retour : un chemin qui ne lève pas l'avis.")
    exige("BeginEdit" not in c, f"« {methode} » rouvre une transaction lui-même : à l'aveugle, sans savoir qui est encore actif.")

appels = sum(f.read_text(encoding="utf-8").count("AviserLesProprietaires();") for f in VM.glob("MainWindowViewModel*.cs"))
exige(appels == 2, f"l'avis est levé {appels} fois dans le modèle de vue, deux attendues (Annuler, Rétablir).")
print("    a. Annuler et Rétablir referment, puis lèvent l'avis sur chaque chemin, sans rien rouvrir eux-mêmes")

# ------------------------------------------------------------- b. ses trois auditeurs

for nom in ("MainWindow.axaml.cs", "PageCanvas.axaml.cs", "BarreContextuelle.axaml.cs"):
    code = (VUES / nom).read_text(encoding="utf-8")
    for sens in ("+=", "-="):
        n = code.count(f"_vm.TransactionsRefermees {sens} OnTransactionsRefermees;")
        exige(n == 1, f"{nom} : « TransactionsRefermees {sens} » {n} fois, une attendue.")
    exige(corps(code, "private void OnTransactionsRefermees(object? sender, EventArgs e)") is not None,
          f"{nom} : pas de réponse à l'avis.")
print("    b. la fenêtre, la planche et la barre écoutent l'avis, et s'en désabonnent")

# ------------------------------------------- c. la saisie suit le focus de sa zone (Q25)

planche = (VUES / "PageCanvas.axaml.cs").read_text(encoding="utf-8")
debut = corps(planche, "private void BeginTextEdit(TextFrame frame)")
fin = corps(planche, "public void EndTextEdit()")
exige(debut is not None and fin is not None, "BeginTextEdit ou EndTextEdit est introuvable dans PageCanvas.axaml.cs.")
if debut is not None and fin is not None:
    exige("BeginEdit" not in debut and "OuvrirLaTransactionDeLaSaisie" not in debut,
          "BeginTextEdit ouvre la transaction de la saisie : elle doit s'ouvrir au focus de la zone.")
    exige("CommitEdit" not in fin and "FermerLaTransactionDeLaSaisie();" in fin,
          "EndTextEdit ne referme pas la seule transaction que la saisie tient.")
for evenement, sens in (("GotFocusEvent", "focus: true"), ("LostFocusEvent", "focus: false")):
    exige(re.search(rf"AddHandler\({evenement}, \(_, e\) => SuivreLeFocusDeLaSaisie\(e, {sens}\)", planche),
          f"la planche ne suit plus « {evenement} » de la zone de saisie.")
barre = (VUES / "BarreContextuelle.axaml").read_text(encoding="utf-8")
n = barre.count('Opened="SurCouleurOuverte" Closing="SurCouleurQuiSeFerme" Closed="SurCouleurFermee"')
exige(n == 2, f"{n} volet(s) de couleur referment leur transaction à « Closing », deux attendus.")
print("    c. la saisie ouvre et referme sa transaction au focus de sa zone ; la couleur referme la sienne avant de rendre le focus")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : l'avis, levé sur chaque chemin, et ses trois auditeurs ; la saisie suit le focus de sa zone"

# ------------------------------------------------------------------------ 2. les tests

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

jouer "poste=Q24" 9 "2. Annuler et Rétablir, au clavier : la saisie, le champ, le glissé et le sélecteur encore actifs rouvrent leur transaction, et rien d'autre"

# Les voisins : un geste, un pas, où que soit le champ — chaque champ du dock et de la barre
# en fait un (I2, I3), un raccourci pendant la saisie d'un champ fait le sien (I2, I3).
voisins="FullyQualifiedName~DispositionTests.Chaque_champ_du_dock_quel_que_soit_son_onglet_fait_un_seul_pas"
voisins+="|FullyQualifiedName~DispositionTests.Ctrl_G_pendant_une_saisie_du_dock_fait_son_propre_pas"
voisins+="|FullyQualifiedName~BarreContextuelleTests.Chaque_controle_de_la_barre_fait_un_seul_pas"
voisins+="|FullyQualifiedName~BarreContextuelleTests.Ctrl_U_pendant_une_saisie_de_la_barre_fait_son_propre_pas"
jouer "$voisins" 4 "   les voisins : un pas par champ (I2, I3), un par raccourci"

jouer "poste=Q25" 5 "3. un réglage pendant la saisie, à la barre, au dock ou au sélecteur de couleur, fait son propre pas ; le gras aussi, sans prendre le focus ; sous Ctrl+Z, seul le champ au focus rouvre sa transaction"

echo "Q24 : Annuler et Rétablir ne laissent plus une saisie, un champ, un glissé ni le sélecteur de couleur hors de toute transaction."
echo "Q25 : un réglage fait pendant une saisie sur la page a son propre pas."
