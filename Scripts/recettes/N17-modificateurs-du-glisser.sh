#!/usr/bin/env bash
#
# Recette du poste N17 — les modificateurs du glisser, et le rectangle de sélection (fiche 23,
# c8, c10 à c14).
#
# La planche ne lisait qu'Alt pendant un déplacement (ApplyMove) ; Ctrl et Maj basculaient le
# bloc dans la sélection à la pression, et rien d'autre ; la seule copie était Ctrl+D, décalée de
# 12 pt ; une pression dans le vide vidait la sélection ; le curseur de rotation était une main.
# Publisher copie au Ctrl+glisser, tient l'axe au Maj+glisser, trace un carré sous Maj et depuis
# le centre sous Ctrl, redimensionne depuis le centre sous Ctrl, prend au rectangle de sélection,
# et montre une flèche circulaire sur la poignée de rotation. Deux temps.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. la mécanique vit dans PageCanvas.Gestes.cs, partie de la planche, inscrite à la liste
#         des vues de la recette I1 ;
#      b. les trois cibles du juge que nomme le piège du plan restent dans PageCanvas.axaml.cs,
#         une fois chacune : la ligne qui trouve le bloc atteint, la garde de l'outil armé sur
#         le gris, le chemin du pointeur dans le repère du bloc qu'on redimensionne ;
#      c. Ctrl et Maj ne basculent plus le bloc à la pression : OnPointerPressed passe la main à
#         PrendreSousUnModificateur, et la bascule se fait au relâché, après la transaction ;
#         la pression dans le vide tire le rectangle ;
#      d. ApplyMove tient l'axe sous Maj ; ApplyResize redimensionne depuis le centre sous Ctrl,
#         sans recopier la ligne visée ; le tracé passe par CoinsDuTrace, à l'aperçu et au bloc ;
#      e. le rectangle se pose dans l'Overlay, aux pinceaux de l'apparence lus par le code ;
#      f. le curseur de rotation est le tracé de l'icône de rotation de l'apparence, et la main
#         du jeu standard a quitté la planche ;
#      g. Échap pendant un glissé l'annule, dans la planche, avant la sortie de la saisie ;
#      h. le README dit les gestes, au sous-tableau « Souris ».
#
# 2. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre, au pointeur et
#    au clavier synthétisés (trait « poste=N17 » : ModificateursDuGlisserTests) — la recette du
#    plan :
#
#      · Ctrl+glisser donne deux blocs en un pas, l'original à sa place ; sur un bloc non pris,
#        toute la sélection et lui ; un Ctrl+clic bascule au relâché, et ne copie rien ;
#      · Maj+glisser garde Y, ou X, et l'axe tenu ne s'aimante pas ;
#      · un tracé sous Maj a une largeur égale à sa hauteur, carré ou cercle, à l'aperçu comme au
#        bloc ; sous Ctrl, la pression est le centre ;
#      · Ctrl sur une poignée garde le centre, sur un bloc droit comme tourné ;
#      · un rectangle prend deux blocs sur trois, même tiré depuis le gris, même à rebours, sans
#        le bloc masqué ; Maj ou Ctrl l'ajoute ; il se dessine dans l'Overlay ;
#      · Échap après un glissé rend le bloc à sa place, sans pas d'annulation ; de même pour une
#        copie, un redimensionnement, une rotation, un rectangle ;
#      · le curseur de rotation est dessiné, et n'est aucun curseur du jeu standard.
#
#    Le compte des tests joués est vérifié : un filtre qui ne désigne plus rien sort en 0. Le
#    test de C3 — un clic et trois Maj+clics prennent quatre blocs, sans pas — est joué par la
#    recette C3, que Maj changeant de sens à la pression ne devait pas faire tomber.
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

def corps(texte, signature):
    trouve = re.search(re.escape(signature) + r"\n    \{(.*?)\n    }\n", texte, re.S)
    return trouve.group(1) if trouve else None

planche = lire("MyPublisher/Views/PageCanvas.axaml.cs")
gestes_chemin = racine / "MyPublisher/Views/PageCanvas.Gestes.cs"
gestes = gestes_chemin.read_text(encoding="utf-8") if gestes_chemin.exists() else ""
toutes = {f.name: f.read_text(encoding="utf-8") for f in sorted((racine / "MyPublisher/Views").glob("PageCanvas*.cs"))}

# a. la mécanique, et la recette I1.
exige(gestes != "", "PageCanvas.Gestes.cs n'existe pas.")
exige("public partial class PageCanvas" in gestes, "PageCanvas.Gestes.cs n'est pas une partie de la planche.")
recette_i1 = lire("Scripts/recettes/I1-apparence.sh")
liste = re.search(r"vues_de_la_phase=\((.*?)\)", recette_i1, re.S).group(1).split()
exige("MyPublisher/Views/PageCanvas.Gestes.cs" in liste,
      "PageCanvas.Gestes.cs n'est pas dans la liste des vues de la phase de la recette I1.")
print("    a. PageCanvas.Gestes.cs, partie de la planche, inscrite à la recette I1")

# b. les cibles du piège.
for cible in ("var hit = DansLaPage(page) ? _vm.CurrentPage.Frames",
              "if (!DansLaPage(page)) return;",
              "var localX = dx * cos + dy * sin;"):
    exige(planche.count(cible) == 1, f"la cible du juge « {cible} » n'est plus dans PageCanvas.axaml.cs une fois.")
    for nom, texte in toutes.items():
        if nom != "PageCanvas.axaml.cs":
            exige(cible not in texte, f"{nom} recopie la cible du juge « {cible} ».")
print("    b. les trois cibles du piège, une fois chacune, et nulle part ailleurs")

# c. la pression sous Ctrl et Maj, et dans le vide.
presse = corps(planche, "private void OnPointerPressed(object? sender, PointerPressedEventArgs e)")
exige(presse is not None, "OnPointerPressed n'est plus dans la planche.")
if presse:
    exige("PrendreSousUnModificateur(hit, page, e);" in presse,
          "OnPointerPressed ne passe plus la pression sous Ctrl ou Maj à PrendreSousUnModificateur.")
    exige("ToggleSelection" not in presse, "OnPointerPressed bascule encore le bloc à la pression.")
    exige("CommencerLeRectangle(page, additive, e);" in presse, "la pression dans le vide ne tire plus de rectangle.")
lache = corps(planche, "private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)")
exige(lache is not None and lache.find("_vm?.CommitEdit();") < lache.find("BasculerAuRelache();"),
      "le relâché ne bascule plus le bloc après avoir refermé la transaction.")
exige("_vm?.ToggleSelection(bloc);" in gestes, "le relâché d'un Ctrl+clic ne bascule plus le bloc.")
exige("if (_copieAuGlisse) CopierLaSelectionSurPlace(feuille);" in gestes, "Ctrl+glisser ne copie plus.")
print("    c. Ctrl et Maj attendent le relâché ; Ctrl+glisser copie ; la pression dans le vide tire le rectangle")

# d. Maj, Ctrl et le tracé.
deplace = corps(planche, "private void ApplyMove(Point page, KeyModifiers modifiers)")
exige(deplace is not None and "(dx, dy) = TenirLAxe(dx, dy);" in deplace, "ApplyMove ne tient plus l'axe sous Maj.")
redim = corps(planche, "private void ApplyResize(Frame frame, Point page, bool keepAspect)")
exige(redim is not None and "DepuisLeCentre(localX, _startWidth, movesLeft, movesRight)" in redim,
      "ApplyResize ne redimensionne plus depuis le centre sous Ctrl.")
exige("_depuisLeCentre = e.KeyModifiers.HasFlag(KeyModifiers.Control);" in planche,
      "la planche ne lit plus Ctrl sur une poignée.")
exige(planche.count("CoinsDuTrace(_startPointer,") == 2, "l'aperçu et le bloc tracé ne passent plus tous deux par CoinsDuTrace.")
print("    d. Maj tient l'axe ; Ctrl redimensionne depuis le centre ; le tracé passe par CoinsDuTrace")

# e. le rectangle, dans l'Overlay, aux pinceaux de l'apparence.
exige("Overlay.Children.Add(_rectangleDeSelection);" in gestes, "le rectangle de sélection n'est plus posé dans l'Overlay.")
exige('PinceauDeLApparence("Accent")' in gestes and 'PinceauDeLApparence("Projection")' in gestes,
      "le rectangle ne prend plus les pinceaux de l'apparence.")
exige("PageRoot" not in re.sub(r"///.*|//.*", "", gestes), "PageCanvas.Gestes.cs touche à PageRoot (règle 4).")
print("    e. le rectangle se pose dans l'Overlay, à l'accent et à la projection de l'apparence")

# f. le curseur de rotation.
exige('TraceDeLIcone("IconeRotation")' in gestes, "le curseur de rotation n'est plus le tracé de l'icône de rotation.")
for nom, texte in toutes.items():
    exige("StandardCursorType.Hand" not in texte, f"{nom} emploie encore la main du jeu standard.")
exige("InputLayer.Cursor = CurseurDeRotation;" in planche, "la poignée de rotation ne montre plus le curseur dessiné.")
print("    f. le curseur de rotation est l'icône de l'apparence ; la main du jeu standard est partie")

# g. Échap.
touche = corps(planche, "protected override void OnKeyDown(KeyEventArgs e)")
exige(touche is not None and touche.find("AnnulerLeGlisse()") != -1 and
      touche.find("AnnulerLeGlisse()") < touche.find("_editing is not null"),
      "Échap n'annule plus le glissé avant de refermer la saisie.")
print("    g. Échap annule le glissé")

# h. le README.
readme = lire("README.md")
souris = re.search(r"^### Souris$(.*?)^##", readme, re.S | re.M)
exige(souris is not None, "le README n'a plus de sous-tableau « Souris ».")
if souris:
    for ligne in (r"^\| `Ctrl` \+ glisser un bloc \| Copie",
                  r"^\| `Maj` \+ glisser un bloc \| Déplace .*axe dominant",
                  r"^\| Glisser dans le vide ou depuis le gris \| Rectangle de sélection",
                  r"^\| `Échap` pendant un glissé \| L'annule",
                  r"^\| `Maj` en traçant un bloc \| Un carré, un cercle",
                  r"^\| `Ctrl` en traçant un bloc \| Trace depuis le centre",
                  r"^\| `Ctrl` sur une poignée \| Redimensionne depuis le centre"):
        exige(re.search(ligne, souris.group(1), re.M) is not None, f"le README ne dit plus « {ligne} ».")
print("    h. le README dit les gestes, au sous-tableau « Souris »")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : la partie Gestes, les cibles du piège, Ctrl et Maj, le tracé, le rectangle, le curseur, Échap, le README"

# ------------------------------------------------------------------------ 2. les gestes

attendus=17
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=N17" > "$travail/tests.txt" 2>&1; then
    grep -E "Failed |Error Message|Assert" -A3 "$travail/tests.txt" | sed 's/^/    /' >&2 || sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les modificateurs du glisser (« poste=N17 ») ne passent pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=N17 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  2. les gestes sur la vraie fenêtre : $joues tests (Ctrl+glisser, Maj+glisser, le tracé, Ctrl sur une poignée, le rectangle, Échap, le curseur)"

echo "N17 : Ctrl+glisser copie, Maj+glisser tient l'axe, Maj et Ctrl au tracé et sur une poignée, un rectangle prend les blocs tout entiers dedans, Échap annule le glissé, le curseur de rotation est la flèche de l'apparence."
