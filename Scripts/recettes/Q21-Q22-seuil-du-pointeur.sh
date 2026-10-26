#!/usr/bin/env bash
#
# Recette des postes Q21 et Q22 — le seuil du pointeur : un tremblement ne déplace pas le
# bloc, un clic sur une poignée ne redimensionne pas.
#
# Q14 avait gardé le bloc d'un clic sans mouvement, mais un pointeur réel tremble : un pixel
# de mouvement pendant le clic, près d'une marge, faisait sauter le bloc de 3 pt par
# l'aimantation et empilait un pas d'annulation (Q21) ; une pression sans mouvement sur une
# poignée repassait par les cosinus d'un bloc tourné, qui en ressortait décalé avec un pas
# empilé, et Maj ramenait à 0° un bloc à 7° sans le moindre mouvement (Q22). La planche ne compte désormais un geste
# pour un glissé qu'une fois le pointeur écarté de trois pixels d'écran de sa prise, pour tout
# glissé — déplacement, poignées, rotation, extrémités de trait — ; l'aimantation ne joue
# qu'au-delà. Deux temps.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. le seuil est déclaré une fois, à trois pixels d'écran, dans la planche ;
#      b. la pression retient sa prise dans le repère de Surface — en pixels d'écran, et non
#         en points de page — et repart d'un geste qui n'a pas glissé ;
#      c. OnPointerMoved ne laisse passer au déplacement, aux poignées, à la rotation et aux
#         extrémités d'un trait qu'un pointeur qui a glissé : la garde est une, posée après
#         le repère et avant le déplacement ;
#      d. les cibles du juge de Q14 restent, une fois chacune : la garde du mouvement nul
#         (« if (dx == 0 && dy == 0) ») et la remise du bloc à son départ
#         (« frame.X = startX; ») ;
#      e. règle 14 : les tests qui jouaient un glissé d'un pixel tout droit — celui de Q14,
#         celui de Q24 — le mènent au-delà du seuil puis le ramènent, par l'aide du banc.
#
# 2. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre, au pointeur
#    synthétisé (« SeuilDuPointeurTests », trait « poste=Q21 ») :
#
#      · un clic qui tremble d'un pixel près d'une marge — dans chaque direction, et en
#        diagonale jusqu'à 2,8 px — laisse le bloc à sa place et n'empile rien ;
#      · le seuil se compte en pixels d'écran : deux pixels à 50 % ne déplacent rien, quatre
#        pixels à 200 % déplacent de deux points ;
#      · un glissé d'un point, mené au-delà du seuil puis ramené, déplace encore le bloc d'un
#        point, sans Alt, et fait son pas ;
#      · un glissé ramené exactement à sa prise, près d'une marge, rend le bloc sans
#        l'aimanter : c'est désormais le seul chemin qui mène à la garde de Q14 ;
#      · l'extrémité d'un trait qui tremble reste où elle est ;
#      · en saisie, le clic qui en sort sur un autre bloc et tremble ne le déplace pas ;
#
#    et sous « poste=Q22 » :
#
#      · une pression puis un relâchement sur chacune des huit poignées d'un bloc droit, avec
#        le mouvement nul d'une plate-forme ou en tremblant d'un pixel, laissent le bloc
#        identique et l'historique vide ; une pression à quatre pixels du coin sud-est aussi ;
#      · un bloc tourné de 12,3456789°, pressé de même sur chacune de ses neuf poignées, la
#        rotation comprise, ne bouge pas ;
#      · la poignée de rotation pressée sous Maj, sans mouvement ou en tremblant, laisse 7° à
#        7° ; menée au-delà du seuil puis ramenée, elle arrondit encore, à 0°.
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
import pathlib, re, sys

racine = pathlib.Path(sys.argv[1])
echecs = []

def exige(condition, message):
    if not condition:
        echecs.append(message)

def lire(chemin):
    return (racine / chemin).read_text(encoding="utf-8")

planche = lire("MyPublisher/Views/PageCanvas.axaml.cs")

# a. le seuil.
exige(planche.count("private const double SeuilDuGlisse = 3;") == 1,
      "le seuil du glissé n'est pas déclaré une fois, à trois pixels, dans la planche.")
print("    a. le seuil du glissé est déclaré une fois, à trois pixels d'écran")

# b. la prise, en pixels d'écran.
presse = re.search(r"private void OnPointerPressed\(object\? sender, PointerPressedEventArgs e\)\n    \{(.*?)\n    }\n", planche, re.S)
exige(presse is not None, "OnPointerPressed n'est plus dans la planche.")
if presse:
    corps = presse.group(1)
    exige(re.search(r"var surfacePoint = e\.GetPosition\(Surface\);.*?_priseALEcran = surfacePoint;\s*_aGlisse = false;", corps, re.S) is not None,
          "la pression ne retient pas sa prise en pixels d'écran, ou ne repart pas d'un geste qui n'a pas glissé.")
aglisse = re.search(r"private bool AGlisse\(Point surfacePoint\)(.*?)\n    }\n", planche, re.S)
exige(aglisse is not None and "surfacePoint - _priseALEcran" in aglisse.group(1)
      and "SeuilDuGlisse * SeuilDuGlisse" in aglisse.group(1),
      "AGlisse ne mesure pas l'écart du pointeur à sa prise, en pixels d'écran, contre le seuil.")
print("    b. la pression retient sa prise dans le repère de Surface, en pixels d'écran")

# c. la garde, une, entre le repère et le déplacement.
bouge = re.search(r"private void OnPointerMoved\(object\? sender, PointerEventArgs e\)\n    \{(.*?)\n    }\n", planche, re.S)
exige(bouge is not None, "OnPointerMoved n'est plus dans la planche.")
if bouge:
    corps = bouge.group(1)
    exige(corps.count("if (!AGlisse(surfacePoint)) return;") == 1,
          "OnPointerMoved ne garde pas le glissé par le seuil, une fois.")
    garde = corps.find("if (!AGlisse(surfacePoint)) return;")
    exige(corps.find("ApplyGuideMove(") < garde < corps.find("ApplyMove(") < corps.find("ApplyRotate(")
          and garde < corps.find("TirerUneExtremite(") and garde < corps.find("ApplyResize("),
          "la garde du seuil n'est pas posée avant le déplacement, les poignées, la rotation et les extrémités.")
print("    c. la garde du seuil précède le déplacement, les poignées, la rotation et les extrémités d'un trait")

# d. les cibles du juge de Q14.
exige(planche.count("if (dx == 0 && dy == 0)") == 1, "la garde du mouvement nul de Q14 n'est plus là, une fois.")
exige(planche.count("frame.X = startX;") == 1, "la remise du bloc à son départ (Q14) n'est plus là, une fois.")
print("    d. les cibles du juge de Q14 restent, une fois chacune")

# e. règle 14.
double_clic = lire("MyPublisher.Tests.Interface/DoubleClicTests.cs")
glisse = re.search(r"public void Un_glisse_dun_point_empile_toujours_son_pas\(\)(.*?)\n    }\n", double_clic, re.S)
exige(glisse is not None and "Rendu.GlisserEtRamener(fenetre, centre, arrivee, RawInputModifiers.Alt);" in glisse.group(1),
      "le glissé d'un point de Q14 n'est pas mené au-delà du seuil puis ramené.")
transactions = lire("MyPublisher.Tests.Interface/TransactionsDeSaisieTests.cs")
exige("Rendu.GlisserEtRamener(fenetre, prise, prise + new Point(1, 0), RawInputModifiers.Alt);" in transactions,
      "le glissé d'un point de Q24 n'est pas mené au-delà du seuil puis ramené.")
print("    e. règle 14 : les glissés d'un pixel de Q14 et de Q24 passent le seuil, puis reviennent")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : le seuil, la prise en pixels d'écran, la garde, les cibles de Q14, la règle 14"

# ------------------------------------------------------------------------ 2. les gestes

jouer() {
    local poste="$1" attendus="$2" quoi="$3"
    if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
            --nologo -v q --filter "poste=$poste" > "$travail/tests-$poste.txt" 2>&1; then
        sed 's/^/    /' "$travail/tests-$poste.txt" >&2
        echo "ÉCHEC : les gestes du seuil du pointeur (« poste=$poste ») ne passent pas." >&2
        exit 1
    fi
    local joues
    joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests-$poste.txt" | head -1)"
    if [ "${joues:-0}" -lt "$attendus" ]; then
        echo "ÉCHEC : $attendus tests attendus sous « poste=$poste », ${joues:-0} joués." >&2
        echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
        exit 1
    fi
    echo "  2. $poste, les gestes sur la vraie fenêtre : $joues tests ($quoi)"
}

jouer Q21 6 "le clic qui tremble près d'une marge, le seuil en pixels d'écran, le glissé d'un point ramené, le glissé revenu à sa prise, l'extrémité d'un trait, le clic qui sort de la saisie"
jouer Q22 3 "les huit poignées d'un bloc droit, les neuf d'un bloc tourné, la rotation sous Maj"

echo "Q21 et Q22 : un clic qui tremble de moins de trois pixels ne déplace rien, ne redimensionne rien, ne tourne rien ; au-delà, le glissé compte, d'un point s'il le faut."
