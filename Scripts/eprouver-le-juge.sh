#!/usr/bin/env bash
#
# Éprouve le filet en le trouant : sabote le code, une chose à la fois, et vérifie
# que quelque chose s'en aperçoit.
#
# C'est la seule épreuve qui compte vraiment. Un contrôle qui passe ne prouve rien
# tant qu'on ne l'a pas vu échouer — le poste P2 l'avait fait à la main en son temps
# (« le contrôle a été éprouvé en le faisant échouer »), sans rien laisser derrière.
# Ceci le laisse.
#
# Chaque sabotage remplace un bout de code par un autre, joue la vérification, et
# attend un échec. Un sabotage NON détecté est un trou dans le filet : le script le
# nomme et sort en erreur.
#
#   Scripts/eprouver-le-juge.sh
#
# L'arbre de travail doit être propre : le script écrit dans les sources et les
# restaure par « git checkout ». Il refuse de tourner autrement, pour ne pas
# emporter un travail en cours.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$racine"

if [ -n "$(git status --porcelain)" ]; then
    echo "ÉCHEC : l'arbre de travail n'est pas propre." >&2
    echo "Ce script sabote les sources et les restaure par « git checkout » :" >&2
    echo "il emporterait le travail en cours." >&2
    exit 2
fi

# Verrou. Ce script écrit dans les sources et les restaure par « git checkout » :
# quiconque travaillerait dans l'arbre pendant qu'il tourne verrait son travail
# emporté sans un mot. C'est arrivé une fois, le 6 septembre 2026, à deux fichiers
# de documentation — et rien ne l'avait signalé.
verrou="$racine/.juge-en-cours"
if [ -e "$verrou" ]; then
    echo "ÉCHEC : une épreuve du juge est déjà en cours ($verrou)." >&2
    echo "Ce script restaure l'arbre en sortant : deux instances se marcheraient dessus." >&2
    exit 2
fi
touch "$verrou"

restaurer() { git checkout -- . 2>/dev/null || true; }
nettoyer() { restaurer; rm -f "$verrou"; }
trap nettoyer EXIT

trous=0
sabotages=0

# $1 intitulé · $2 fichier · $3 texte d'origine · $4 texte saboté
saboter() {
    local intitule="$1" fichier="$2" avant="$3" apres="$4"
    sabotages=$((sabotages + 1))

    if ! grep -qF "$avant" "$fichier"; then
        echo "  ?  $intitule — le code visé a changé, sabotage impossible" >&2
        echo "     ($fichier ne contient plus « ${avant:0:60}… »)" >&2
        trous=$((trous + 1))
        return
    fi

    python3 - "$fichier" "$avant" "$apres" <<'PY'
import sys, pathlib
f, avant, apres = sys.argv[1], sys.argv[2], sys.argv[3]
p = pathlib.Path(f)
p.write_text(p.read_text().replace(avant, apres, 1))
PY

    if Scripts/verifier.sh > /tmp/juge-$$.txt 2>&1; then
        echo "  ✗  $intitule — NON DÉTECTÉ" >&2
        trous=$((trous + 1))
    else
        # Qui l'a attrapé, précisément. Un nom de test s'il y en a un ; sinon
        # l'étape qui a crié. Sans cette précision, on croirait le filet plus
        # large qu'il n'est — trois sabotages « attrapés par le contrôle »
        # peuvent n'être qu'un seul et même garde-fou.
        coupable="$(grep -oE "[A-Za-z_]+Tests\.[A-Za-z_]+" /tmp/juge-$$.txt | head -1 || true)"
        [ -z "$coupable" ] && coupable="$(grep -oE '^ÉCHEC : .*' /tmp/juge-$$.txt | head -1 || true)"
        [ -z "$coupable" ] && coupable="$(grep -oE '  [a-z0-9-]+ : ÉCHOUE' /tmp/juge-$$.txt | head -1 || true)"
        [ -z "$coupable" ] && coupable="la compilation"
        echo "  ✓  $intitule"
        echo "       attrapé par : $coupable"
    fi

    rm -f /tmp/juge-$$.txt
    restaurer
}

echo "Épreuve du juge — chaque ligne sabote une chose et attend un échec."
echo

saboter "l'aimantation ne s'accroche plus" \
    MyPublisher.Core/Models/Snap.cs \
    'var bestDistance = tolerance;' \
    'var bestDistance = 0.0;'

saboter "une copie de bloc perd son interligne" \
    MyPublisher.Core/Models/Frame.cs \
    'LineHeight = LineHeight,' \
    ''

saboter "la migration de la version 1 ne fait plus rien" \
    MyPublisher.Core/Services/FormatMigrations.cs \
    'if (version < 2) ToVersion2(root);' \
    ''

saboter "le JSON n'est plus écrit en clair" \
    MyPublisher.Core/Services/DocumentSerializer.cs \
    'Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping' \
    'Encoder = null'

saboter "un groupe ne met plus ses blocs à l'échelle" \
    MyPublisher.Core/Models/Frame.cs \
    'var factor = newSize / oldSize;' \
    'var factor = 1.0;'

saboter "la justification ne repartit plus le blanc" \
    MyPublisher.Core/Models/Compositeur.cs \
    "var part = manque / blancs;" \
    "var part = 0.0;"

saboter "l interligne redevient un multiple du corps" \
    MyPublisher.Core/Models/Compositeur.cs \
    "naturelle * (multiple > 0 ? multiple : 1);" \
    "multiple;"

# Le poste Q1. Quatre sabotages et non un : le calcul peut être juste et le
# branchement faux, et chacun des deux se casse à sa façon. La recette de PLAN.md
# demande que neutraliser le décalage fasse tomber le test pur, le test d'interface et
# la recette ; le premier sabotage le fait, les trois autres trouent le branchement.
saboter "taper devant un fragment ne le décale plus" \
    MyPublisher.Core/Models/Recalage.cs \
    '(debut, fin) = (debut + edition.Inseres, fin + edition.Inseres);' \
    '(debut, fin) = (debut, fin);'

saboter "un saut de ligne tapé ne décale plus les écarts de paragraphe" \
    MyPublisher.Core/Models/Recalage.cs \
    'Inseres: Sauts(apres, point, point + edition.Inseres),' \
    'Inseres: 0,'

saboter "la saisie sur la page ne passe plus par le recalage" \
    MyPublisher/Views/PageCanvas.axaml \
    'Text="{Binding Saisie, Mode=TwoWay}"' \
    'Text="{Binding Text, Mode=TwoWay}"'

saboter "une case de tableau ne recale plus ses fragments à la saisie" \
    MyPublisher.Core/Models/Table.cs \
    'Recalage.Appliquer(Text, nouveau, Spans, null);' \
    ''

echo
if [ "$trous" = 0 ]; then
    echo "Le juge sait échouer : $sabotages sabotages, $sabotages attrapés."
else
    echo "TROUS DANS LE FILET : $trous sabotage(s) sur $sabotages non détecté(s)." >&2
    echo "Un sabotage non détecté est une régression que la boucle laisserait passer." >&2
    exit 1
fi
