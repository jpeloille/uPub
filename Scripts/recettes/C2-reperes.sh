#!/usr/bin/env bash
#
# Recette du poste C2 — repères : grille de composition (C2a), règles et repères
# libres (C2b).
#
# Deux temps, comme les deux recettes d'origine, qui ont été jouées puis jetées.
#
# 1. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre, au
#    pointeur synthétisé (« ReperesTests », trait « poste=C2 ») :
#
#      · quatre blocs lâchés à moins de 4 pt du bord de la deuxième colonne, de part
#        et d'autre, s'y posent tous EXACTEMENT ;
#      · Alt désarme l'aimantation des blocs comme celle des repères ;
#      · un repère tiré de la règle se pose, se trace là où il a été posé, rallume les
#        repères et s'annule ;
#      · lâché près d'un bord de colonne, il s'y aimante ;
#      · on le reprend sur la page, on le retire en le ramenant hors de la page, et
#        les deux gestes se défont chacun d'un pas ;
#      · tiré de la règle de GAUCHE, c'est un repère horizontal : il s'aimante sur un
#        bord de ligne de la grille, se retire au-dessus de la page, et chaque geste
#        s'annule ;
#      · un bloc vient se poser exactement sur un repère choisi — 333,3 pt, une
#        position que ni la grille ni les marges ne proposent, et qu'aucun pointeur ne
#        viserait au chiffre.
#
#    Le compte des tests joués est vérifié : un filtre qui ne désigne plus rien sort en
#    0, et serait la seule façon pour cette recette de se dire verte sans avoir rien vu.
#
# 2. RIEN DE TOUT CELA NE S'IMPRIME. Le document d'exemple est exporté trois fois :
#    sans grille ; avec la grille la plus dense que le panneau laisse régler — 24
#    colonnes et 24 lignes à gouttière, une ligne de base tous les points, soit près
#    de huit cents repères ; et la même grille avec des repères libres. Les trois PDF
#    doivent donner des pages identiques AU PIXEL PRÈS — sans seuil : les repères ne
#    passent pas par l'export, il n'y a aucun lissage à tolérer.
#
#    Contre-épreuve : trois images identiques ne prouvent rien si la grille n'a jamais
#    été lue. Le même document est donc aussi rendu à l'ÉCRAN, repères allumés, avec
#    et sans grille : là, les deux images doivent différer, et nettement. C'est ce qui
#    prouve que les trois documents portent bien leurs repères, et que l'égalité du
#    point 2 tient à l'export et non à un document qui les aurait perdus.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"
banc="MyPublisher.Banc/bin/Debug/net10.0/MyPublisher.Banc"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# On compile, plutôt que de lancer les tests avec « --no-build » : un binaire périmé
# survit à une modification des sources, et la recette se déclarerait verte sur du code
# qu'elle n'a pas éprouvé (cf. la recette de A4).
if ! dotnet build MyPublisher.sln -c Debug --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

# -------------------------------------------------------------------- 1. les gestes

attendus=7
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj \
        --nologo -v q --filter "poste=C2" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les gestes de C2 ne passent pas." >&2
    exit 1
fi

joues="$(grep -o 'Passed: *[0-9]*' "$travail/tests.txt" | head -1 | grep -o '[0-9]*$' || true)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=C2 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne peut pas s'en contenter." >&2
    exit 1
fi

echo "  1. gestes joués sur la vraie fenêtre : $joues tests (aimantation, Alt, deux règles, reprise, retrait, repère choisi)"

# ---------------------------------------------------- 2. les repères ne s'impriment pas

# Les trois variantes du même document. La grille est la plus dense que le panneau
# accepte : 24 colonnes, 24 lignes (« Maximum="24" » dans MainWindow.axaml), une
# gouttière pour doubler chaque séparation, et un pas de ligne de base d'un point — en
# dessous, le modèle tient la grille pour absente. Les repères libres sont posés à des
# positions quelconques, sur les deux axes.
python3 - Samples/exemple-bulletin.mypub "$travail" <<'PY'
import json, sys

source, travail = sys.argv[1], sys.argv[2]
base = json.load(open(source, encoding="utf-8"))

def ecrire(nom, **reglages):
    document = dict(base)
    document.update(reglages)
    with open(f"{travail}/{nom}.mypub", "w", encoding="utf-8") as f:
        json.dump(document, f, ensure_ascii=False, indent=2)

sans = dict(ColumnCount=1, ColumnSpacing=0, RowCount=1, RowSpacing=0,
            BaselineSpacing=0, BaselineOffset=0,
            VerticalRulerGuides=[], HorizontalRulerGuides=[])
dense = dict(sans, ColumnCount=24, ColumnSpacing=5.67, RowCount=24, RowSpacing=5.67,
             BaselineSpacing=1, BaselineOffset=0.5)
libres = dict(dense,
              VerticalRulerGuides=[{"Position": p} for p in (33.3, 150.5, 297.64, 410, 570.2)],
              HorizontalRulerGuides=[{"Position": p} for p in (20, 133.3, 420.94, 612.7, 800)])

ecrire("sans-grille", **sans)
ecrire("grille-dense", **dense)
ecrire("grille-et-reperes", **libres)
PY

# Le document relu doit porter ce qu'on y a écrit : si le sérialiseur laissait tomber
# la grille, les trois exports seraient identiques pour une mauvaise raison. La
# contre-épreuve à l'écran le verrait aussi ; ceci le dit plus tôt et plus clairement.
"$binaire" "$travail/grille-et-reperes.mypub" --resave "$travail/relu.mypub" > /dev/null
python3 - "$travail/relu.mypub" <<'PY'
import json, sys
d = json.load(open(sys.argv[1], encoding="utf-8"))
porte = (d["ColumnCount"], d["RowCount"], d["BaselineSpacing"],
         len(d["VerticalRulerGuides"]), len(d["HorizontalRulerGuides"]))
if porte != (24, 24, 1, 5, 5):
    sys.exit(f"ÉCHEC : relu, le document ne porte plus sa grille ni ses repères {porte}.")
PY

for nom in sans-grille grille-dense grille-et-reperes; do
    "$binaire" "$travail/$nom.mypub" --export "$travail/$nom.pdf" > /dev/null
    pdftoppm -r 144 -png "$travail/$nom.pdf" "$travail/$nom"
done

pages="$(ls "$travail"/sans-grille-*.png | wc -l)"
if [ "$pages" -lt 2 ]; then
    echo "ÉCHEC : $pages page(s) rendue(s) du document d'exemple, deux attendues." >&2
    exit 1
fi

for image in "$travail"/sans-grille-*.png; do
    suffixe="${image##*/sans-grille-}"

    # Une page blanche serait égale à une autre page blanche : on s'assure d'abord
    # qu'il y a de quoi comparer.
    encre="$(magick "$image" -fuzz 10% -fill black +opaque white -fill white -opaque white \
             -format "%[fx:int(w*h*(1-mean))]" info:)"
    if [ "$encre" -lt 10000 ]; then
        echo "ÉCHEC : la page $suffixe du PDF est presque vide ($encre pixels d'encre)." >&2
        exit 1
    fi

    for autre in grille-dense grille-et-reperes; do
        ecart="$(magick compare -metric AE "$image" "$travail/$autre-$suffixe" null: 2>&1 \
                 | awk '{print $1}' || true)"
        if [ "$ecart" != "0" ]; then
            echo "ÉCHEC : page $suffixe — le PDF « $autre » diffère de celui sans grille" \
                 "($ecart pixels). Un repère s'imprime." >&2
            exit 1
        fi
    done
    echo "  2. page ${suffixe%.png} : sans grille, grille dense, grille et repères libres — 0 pixel d'écart ($encre pixels d'encre)"
done

# -------------------------------------------------------------------- contre-épreuve

# L'écran, repères ALLUMÉS — donc sans « --sans-reperes ». La grille dense pose une
# ligne de base tous les points sur toute la hauteur de la zone de marges : l'écart à
# la page sans grille se compte en centaines de milliers de pixels. Le seuil, à dix
# mille, ne demande que la preuve que la grille est là.
"$banc" rendre "$travail/sans-grille.mypub" --sortie "$travail/ecran-sans.png" --page 1 > /dev/null
"$banc" rendre "$travail/grille-et-reperes.mypub" --sortie "$travail/ecran-reperes.png" --page 1 > /dev/null

visible="$(magick compare -metric AE -fuzz 5% "$travail/ecran-sans.png" "$travail/ecran-reperes.png" null: 2>&1 \
           | awk '{print $1}' || true)"
if ! [[ "$visible" =~ ^[0-9]+(\.[0-9]+)?(e\+[0-9]+)?$ ]] \
   || awk -v v="$visible" 'BEGIN { exit !(v < 10000) }'; then
    echo "ÉCHEC : à l'écran, la grille et les repères ne se voient pas ($visible pixels d'écart)." >&2
    echo "L'égalité des PDF ne prouve alors rien : le document ne porte peut-être plus ses repères." >&2
    exit 1
fi
echo "     contre-épreuve : à l'écran, les mêmes repères changent $visible pixels de la page 1"

echo "C2 : blocs et repères s'aimantent au geste, Alt désarme, et rien de la grille ne s'imprime."
