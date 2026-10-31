#!/usr/bin/env bash
#
# Recette du poste N29 — le clavier suit les lignes composées (fiche 23, a21 ; décidé par le
# propriétaire le 31 octobre 2026 sur la mesure de N21).
#
# Depuis la voie 2a (N21), la saisie montre le texte composé, mais le champ caché gardait la
# navigation au clavier sur ses propres lignes, coupées à sa mesure, sans retraits ni fragments :
# sur ATA 21, depuis le milieu de 33 lignes composées, ↓ tombait ailleurs que là où la composition
# l'attend 21 fois, ↑ 23 fois, Début et Fin 4 fois chacune. La planche calcule désormais ces
# touches sur les lignes de la composition. Deux temps.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. la mécanique vit dans un partiel de la planche, PageCanvas.ClavierDuTexte.cs, et non
#         dans son fichier principal (règle 16) : la touche de la saisie, prise avant le champ
#         (ToucheDeLaSaisie, en tunnel), passe d'abord par les lignes ; Début, Fin, ↑, ↓, Pg.Préc
#         et Pg.Suiv, seules ou sous Maj, y sont calculées et marquées traitées — le champ ne fait
#         pas sa propre navigation ; Ctrl+Début et Ctrl+Fin, Ctrl+← et Ctrl+→ lui restent ;
#      b. la géométrie du texte (TexteCompose.cs) dit la ligne qu'une coupure douce finit,
#         la ligne du curseur selon son affinité, la position d'une ligne à une abscisse, le bout
#         d'une ligne et le décalage de sa colonne ; les marques dessinent le curseur sur la ligne
#         que l'affinité désigne ;
#      c. la fenêtre de candidats de la méthode d'entrée : le client du champ est enveloppé au
#         passage montant de sa demande, et son rectangle de curseur pris au curseur dessiné ;
#      d. le partiel est à la recette I1 ; le README dit les touches, au sous-tableau Texte.
#
# 2. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre (trait
#    « poste=N29 » : ClavierSurLaCompositionTests) — la recette du plan :
#
#      · la mesure de N21 rejouée sur ATA 21, depuis le milieu de chaque ligne composée des
#        blocs non tournés qu'un clic ouvre : aucun écart pour Début, Fin, ↑ et ↓ ;
#      · ↓ ↓ ↑ ↑ revient au caractère de départ, l'abscisse visée gardée sur une ligne courte et
#        perdue au premier autre déplacement ; Maj+↓ étend la sélection d'une ligne composée ;
#      · Fin sur une ligne coupée en douceur dessine le curseur au bout de la ligne, après
#        l'espace où elle se coupe, et non au début de celle d'en dessous ;
#      · dans un bloc à deux colonnes, ↓ sur la dernière ligne de la première mène à la première
#        de la seconde ; Pg.Préc, Pg.Suiv, les bouts du texte, Ctrl+Début et Ctrl+Fin ;
#      · un bloc tourné de 30° navigue comme à plat ;
#      · la fenêtre de candidats se pose au curseur dessiné, et le suit.
#
#    Le compte des tests joués est vérifié : un filtre qui ne désigne plus rien sort en 0.
#
# Au juge : ↓ rendu au champ.
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
import pathlib, re, sys

racine = pathlib.Path(sys.argv[1])
echecs = []

def exige(condition, message):
    if not condition:
        echecs.append(message)

def lire(chemin):
    chemin = racine / chemin
    return chemin.read_text(encoding="utf-8") if chemin.exists() else ""

def corps(texte, signature):
    trouve = re.search(re.escape(signature) + r"(.*?)\n    }\n", texte, re.S)
    return trouve.group(1) if trouve else ""

planche = lire("MyPublisher/Views/PageCanvas.axaml.cs")
saisie = lire("MyPublisher/Views/PageCanvas.Saisie.cs")
clavier = lire("MyPublisher/Views/PageCanvas.ClavierDuTexte.cs")
compose = lire("MyPublisher/Views/TexteCompose.cs")

# a. la mécanique, dans son partiel ; la touche prise avant le champ.
exige(clavier != "", "PageCanvas.ClavierDuTexte.cs n'existe pas.")
for nom in ("ToucheDesLignes", "LigneVisee", "ClientAuCurseurDessine"):
    exige(nom not in planche, f"le fichier principal de la planche porte « {nom} » (règle 16).")
attache = corps(saisie, "protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)")
exige("AddHandler(KeyDownEvent, ToucheDeLaSaisie, RoutingStrategies.Tunnel);" in attache
      and "BrancherLeClavierDesLignes();" in attache,
      "la touche de la saisie n'est plus prise en tunnel, ou le clavier des lignes n'est pas branché.")
touche = corps(saisie, "private void ToucheDeLaSaisie(object? sender, KeyEventArgs e)")
exige(re.search(r"if \(ToucheDesLignes\(zone, e\)\) return;\s*\n\s*if \(e\.KeyModifiers != KeyModifiers\.Shift\) return;", touche),
      "la touche de la saisie ne passe pas par les lignes composées avant le reste.")
lignes = corps(clavier, "private bool ToucheDesLignes(TextBox zone, KeyEventArgs e)")
touches = re.search(r"e\.Key is not \(([^)]*)\)", lignes)
exige(touches is not None and sorted(t.strip() for t in touches.group(1).split(" or ")) ==
      sorted(["Key.Home", "Key.End", "Key.Up", "Key.Down", "Key.PageUp", "Key.PageDown"]),
      "les lignes composées ne prennent pas exactement Début, Fin, ↑, ↓, Pg.Préc et Pg.Suiv.")
exige("e.KeyModifiers != KeyModifiers.None && !maj" in lignes,
      "les lignes composées prennent une touche sous Ctrl ou Alt : Ctrl+Début, Ctrl+Fin et Alt+flèches restent ailleurs.")
exige("e.Handled = true;" in lignes and "PoserLaSelection(zone, maj ? ancre : arrivee.Position, arrivee.Position);" in lignes,
      "la touche calculée ne pose pas la sélection au champ, ou lui laisse sa propre navigation.")
print("    a. PageCanvas.ClavierDuTexte.cs : Début, Fin, ↑, ↓ et les pages pris avant le champ, sous Maj comme sans")

# b. la géométrie et l'affinité.
for signature in ("public int LigneQueFinit(int index)", "public int RangDuCurseur(int index, bool enFinDeLigne)",
                  "public Rect CurseurDe(int index, bool enFinDeLigne)", "public double DecalageDeColonne(int rang)",
                  "public (int Position, bool EnFinDeLigne) SurLaLigne(int rang, double abscisse)",
                  "public (int Position, bool EnFinDeLigne) FinDeLaLigne(int rang)"):
    exige(signature in compose, f"GeometrieDuTexte n'a plus « {signature} ».")
poser = corps(saisie, "private void PoserLesMarquesDuTexte()")
exige("geometrie.CurseurDe(selection.Curseur, enFinDeLigne)" in poser and "CurseurEnFinDeLigne(selection)" in poser,
      "les marques ne dessinent pas le curseur sur la ligne que son affinité désigne.")
print("    b. la géométrie dit la coupure douce, la ligne du curseur et la colonne ; les marques suivent l'affinité")

# c. la méthode d'entrée.
branche = corps(clavier, "private void BrancherLeClavierDesLignes()")
exige("AddHandler(TextInputMethodClientRequestedEvent, EnvelopperLeClientDuChamp, RoutingStrategies.Bubble" in branche,
      "le client de la méthode d'entrée n'est pas pris au passage montant de sa demande.")
exige(re.search(r"public override Rect CursorRectangle => _curseurDessine\(\) \?\? Champ\.CursorRectangle;", clavier),
      "le client de la méthode d'entrée ne lit pas le curseur dessiné.")
exige("DireLeCurseurALaMethode(compose," in poser, "les marques ne disent pas le curseur à la méthode d'entrée.")
print("    c. la fenêtre de candidats lit le curseur dessiné")

# d. la recette I1 et le README.
recette_i1 = lire("Scripts/recettes/I1-apparence.sh")
liste = re.search(r"vues_de_la_phase=\((.*?)\)", recette_i1, re.S).group(1).split()
exige("MyPublisher/Views/PageCanvas.ClavierDuTexte.cs" in liste,
      "PageCanvas.ClavierDuTexte.cs n'est pas dans la liste des vues de la phase de la recette I1.")
readme = lire("README.md")
texte = re.search(r"^### Texte$(.*?)^### ", readme, re.S | re.M)
exige(texte is not None and re.search(
    r"^\| Dans la saisie d'un bloc : `Début` / `Fin`, `↑` / `↓`, `Pg\.Préc` / `Pg\.Suiv` \|", texte.group(1), re.M),
      "le sous-tableau Texte du README ne dit pas Début, Fin, ↑, ↓ et les pages dans la saisie.")
print("    d. le partiel est à la recette I1 ; le README dit les touches")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : le partiel du clavier, la géométrie, la méthode d'entrée, la recette I1, le README"

# ------------------------------------------------------------------------ 2. les gestes

attendus=6
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo --filter "poste=N29" > "$travail/tests.txt" 2>&1; then
    grep -E "Failed |Error Message|Assert" -A3 "$travail/tests.txt" | sed 's/^/    /' >&2 || sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : le clavier sur la composition (« poste=N29 ») ne passe pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=N29 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  2. les gestes sur la vraie fenêtre : $joues tests (la mesure de N21 sur ATA 21 sans écart, l'abscisse visée, Maj+↓, la coupure douce, deux colonnes, les pages, le bloc tourné, la fenêtre de candidats)"

echo "N29 : en saisie, Début, Fin, les flèches et les pages suivent les lignes telles qu'elles s'impriment, et la méthode d'entrée le curseur qu'on voit."
