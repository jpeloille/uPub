#!/usr/bin/env bash
#
# Recette du poste A4 — ordre de superposition : raccourcis et glisser-déposer.
#
# Deux temps, et ils ne se recouvrent pas.
#
# 1. LE TABLEAU DES RACCOURCIS, lu dans les sources. C'est ce qu'un script fait mieux
#    qu'un test : il confronte trois listes écrites à trois endroits par trois mains —
#
#      · les gestes déclarés dans « Window.KeyBindings » ;
#      · les touches que « MainWindow.OnKeyDown » intercepte avant eux ;
#      · le tableau des raccourcis du README, qui est ce que l'utilisateur lit.
#
#    Un raccourci en double se tait : Avalonia joue le premier et laisse l'autre
#    inerte, sans erreur ni avertissement de compilation. Un raccourci absent du README
#    existe sans que personne ne le sache. Aucun des deux n'échoue à l'exécution, et
#    c'est pourquoi ils se vérifient ici.
#
# 2. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre. Un
#    script de shell ne synthétise ni pointeur ni frappe : ce qui demande la fenêtre
#    est éprouvé là-bas, et la recette l'appelle par son nom plutôt que de le déclarer
#    fait. Le compte des tests joués est vérifié — un filtre qui ne désigne plus rien
#    sort en 0, et serait la seule façon pour cette recette de se dire verte sans avoir
#    rien vu.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

tests="MyPublisher.Tests.Interface/bin/Debug/net9.0/MyPublisher.Tests.Interface.dll"
if [ ! -f "$tests" ]; then
    echo "ÉCHEC : tests d'interface non compilés ($tests). Compiler d'abord." >&2
    exit 1
fi

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# --------------------------------------------------- 1. le tableau des raccourcis

echo "  Raccourcis déclarés, interceptés et documentés :"
python3 - MyPublisher/Views/MainWindow.axaml MyPublisher/Views/MainWindow.axaml.cs README.md <<'PY'
import re, sys

axaml, code, readme = (open(chemin, encoding="utf-8").read() for chemin in sys.argv[1:4])
echec = False

bloc = re.search(r"<Window\.KeyBindings>(.*?)</Window\.KeyBindings>", axaml, re.S)
if bloc is None:
    sys.exit("    aucun bloc « Window.KeyBindings » dans MainWindow.axaml")

gestes = re.findall(r'<KeyBinding\s+Gesture="([^"]+)"', bloc.group(1))

# Un doublon ne se signale nulle part : Avalonia retient le premier, et le second ne
# fait jamais rien. C'est le mode de panne que ce poste pouvait introduire.
doublons = sorted({g for g in gestes if gestes.count(g) > 1})
if doublons:
    print("    gestes déclarés deux fois : " + ", ".join(doublons), file=sys.stderr)
    echec = True

# Les touches que la fenêtre intercepte sous Contrôle avant de rendre la main aux
# KeyBindings. Un « Ctrl+S » déclaré comme KeyBinding n'arriverait jamais.
interceptees = set(re.findall(r"case Key\.([A-Z]): On[A-Za-z]+\(", code))
for geste in gestes:
    parties = [p.strip() for p in geste.split("+")]
    if parties[0] == "Ctrl" and len(parties) == 2 and parties[1] in interceptees:
        print(f"    « {geste} » est intercepté par OnKeyDown avant sa commande", file=sys.stderr)
        echec = True

for attendu in ("Ctrl+Shift+F", "Ctrl+Shift+B"):
    if attendu not in gestes:
        print(f"    « {attendu} » n'est pas déclaré", file=sys.stderr)
        echec = True

# Le README doit dire ce que l'application écoute. Il écrit « Maj » là où le XAML écrit
# « Shift » : c'est la seule traduction, et elle est ici plutôt que dans le README, qui
# s'adresse à un lecteur français. Les touches de zoom sont déclarées en double par
# nature — pavé numérique et rangée du haut — et le README les résume d'une ligne ;
# on ne les confronte pas.
zoom = {"Ctrl+OemPlus", "Ctrl+Add", "Ctrl+OemMinus", "Ctrl+Subtract", "Ctrl+D0", "Ctrl+NumPad0"}
confrontes = [g for g in gestes if g not in zoom]
manquants = [g for g in confrontes if g.replace("Shift", "Maj") not in readme]
if manquants:
    print("    absents du tableau du README : " + ", ".join(manquants), file=sys.stderr)
    echec = True

print(f"    {len(gestes)} gestes déclarés, {len(set(gestes))} distincts, "
      f"{len(confrontes)} confrontés au README")
sys.exit(1 if echec else 0)
PY

# -------------------------------------------------------------------- 2. les gestes

attendus=5
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj \
        --no-build --nologo -v q --filter "poste=A4" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les gestes de A4 ne passent pas." >&2
    exit 1
fi

joues="$(grep -o 'Passed: *[0-9]*' "$travail/tests.txt" | head -1 | grep -o '[0-9]*$' || true)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=A4 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne peut pas s'en contenter." >&2
    exit 1
fi

echo "  Gestes joués sur la vraie fenêtre : $joues tests (clavier, glissé, clic simple)"
echo "A4 : raccourcis déclarés sans doublon et documentés, superposition au clavier et à la souris."
