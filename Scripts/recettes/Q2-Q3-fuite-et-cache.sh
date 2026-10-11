#!/usr/bin/env bash
#
# Recette des postes Q2 et Q3 — deux choses que l'écran retenait trop longtemps.
#
# Q2 : le texte composé se détache de son bloc. « ControleDeBloc » s'abonne au bloc et
#      à ses deux collections d'écarts ; il ne s'en défaisait que si on lui retirait
#      son bloc, jamais en quittant l'arbre visuel. Le bloc, qui vit plus longtemps que
#      lui, le retenait alors.
# Q3 : le cache d'images oublie ses échecs. Une image absente à l'ouverture était
#      retenue comme absente pour toute la durée du processus : la remettre en place
#      puis rouvrir le document laissait le bloc vide, sans rien dire.
#
# Deux temps, et ils ne se recouvrent pas.
#
# 1. LA RÈGLE, lue dans les sources. Q2 est la deuxième fois que la même fuite se
#    présente — C5 l'avait refermée sur « FondBloc », un contrôle plus loin — et elle a
#    une forme reconnaissable : un contrôle qui, sur le changement d'une de ses
#    propriétés (« Changed.AddClassHandler »), s'abonne par « += » à l'objet qu'on lui
#    donne. Un tel contrôle doit suivre l'attachement : il surcharge
#    « OnAttachedToVisualTree » ET « OnDetachedFromVisualTree ». La troisième fois
#    viendra d'un aperçu, d'une vignette ou d'un contour de caractère (B6, C7, A14) ;
#    c'est ici qu'elle se fera arrêter. La règle est une heuristique et le dit : elle
#    ne prouve pas qu'un contrôle se désabonne bien, elle prouve qu'il y a pensé.
#    Ce qui le prouve, ce sont les tests du second temps.
#
# 2. LES MESURES, jouées par « MyPublisher.Tests.Interface » sur la vraie fenêtre :
#      · Q2 — vingt changements de page, puis les abonnés comptés par réflexion : sur
#        le bloc affiché, autant de contrôles que de contrôles qui le montrent ; sur
#        celui de la page quittée, aucun. Puis vingt passages d'un contrôle qui quitte
#        l'arbre EN GARDANT son bloc — le cas que le geste réel ne produit pas, le
#        conteneur retirant le bloc de lui-même (cf. PLAN.md, poste Q2), et le seul où
#        le remède se voit. Un contrôle qui reste affiché et change de bloc doit lâcher
#        l'ancien. Et la page revenue doit se redessiner encore quand on la règle : se
#        détacher sans se rattacher rouvrirait le défaut de R1.
#      · Q3 — un document dont les deux images manquent s'ouvre, les fichiers sont
#        remis, le document est rouvert par le geste du menu, dans la même fenêtre :
#        la couleur des deux images se relève dans la page composée. Le bloc image et
#        le fond illustré passent par deux chemins vers le cache ; les deux sont
#        mesurés. Une image déjà lue, elle, n'est pas relue, et un geste annulé entre
#        les deux ne relit pas le disque. Le même test est joué en partant d'un modèle.
#        Enfin, réinsérer l'image remise en place, ou la choisir pour un remplissage,
#        la fait paraître sans rouvrir.
#    Le compte des tests joués est vérifié : un filtre qui ne désigne plus rien sort
#    en 0, et serait la seule façon pour cette recette de se dire verte sans avoir
#    rien vu.
#
# Pourquoi des tests et non le binaire, comme A3 : le cache d'images et les abonnements
# n'existent qu'à l'écran. L'export lit les images sur le disque à chaque fois et ne
# compose aucun contrôle ; un PDF ne peut rien dire de ces deux postes.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# On compile, plutôt que de lancer les tests avec « --no-build » : un binaire périmé
# survit à une modification des sources, et la recette se déclarerait verte sur du
# code qu'elle n'a pas éprouvé. C'est le piège que R3 a relevé.
if ! dotnet build MyPublisher.sln -c Debug --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

# ------------------------------------------------------------------- 1. la règle

echo "  Contrôles qui s'abonnent à ce qu'on leur donne :"
python3 - MyPublisher/Views/*.cs <<'PY'
import re, sys

echec = False
vus = 0
for chemin in sys.argv[1:]:
    source = open(chemin, encoding="utf-8").read()

    # La forme de la fuite : un gestionnaire de changement de propriété, et un
    # abonnement à un événement d'objet. Les « += » d'un modèle de vue (« _vm. ») sont
    # ceux d'une fenêtre ou d'un plan de travail, qui vivent aussi longtemps que lui.
    if ".Changed.AddClassHandler<" not in source:
        continue
    abonnements = [ligne.strip() for ligne in source.splitlines()
                   if re.search(r"\.\w+Changed \+= ", ligne) and "_vm." not in ligne]
    if not abonnements:
        continue

    vus += 1
    nom = chemin.rsplit("/", 1)[-1]
    manque = [m for m in ("OnAttachedToVisualTree", "OnDetachedFromVisualTree")
              if f"override void {m}(" not in source]
    if manque:
        print(f"    {nom} s'abonne ({abonnements[0]}) sans surcharger "
              + " ni ".join(manque), file=sys.stderr)
        echec = True
    else:
        print(f"    {nom} : suit l'attachement ({len(abonnements)} abonnement(s))")

# Au moins les deux contrôles connus : une règle qui ne trouve plus rien à lire
# passerait au vert sans rien avoir regardé.
if vus < 2:
    print(f"    {vus} contrôle(s) reconnu(s), deux attendus au moins (FondBloc, ControleDeBloc)",
          file=sys.stderr)
    echec = True

sys.exit(1 if echec else 0)
PY

# -------------------------------------------------------------------- 2. les mesures

attendus_q2=4
attendus_q3=3
for poste in Q2 Q3; do
    if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj \
            --no-build --nologo -v q --filter "poste=$poste" > "$travail/tests-$poste.txt" 2>&1; then
        sed 's/^/    /' "$travail/tests-$poste.txt" >&2
        echo "ÉCHEC : les mesures de $poste ne passent pas." >&2
        exit 1
    fi

    attendus="attendus_${poste,,}"
    joues="$(grep -o 'Passed: *[0-9]*' "$travail/tests-$poste.txt" | head -1 | grep -o '[0-9]*$' || true)"
    if [ "${joues:-0}" -lt "${!attendus}" ]; then
        echo "ÉCHEC : ${!attendus} tests attendus sous « poste=$poste », ${joues:-0} joués." >&2
        echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne peut pas s'en contenter." >&2
        exit 1
    fi
done

echo "  Q2 : vingt changements de page, aucun contrôle de trop ; un contrôle sorti de l'arbre ou changé de bloc se tait ($attendus_q2 tests)"
echo "  Q3 : des images remises en place reparaissent à la réouverture, au départ d'un modèle, à la réinsertion ; les images lues restent ($attendus_q3 tests)"
echo "Q2-Q3 : le texte composé se détache de son bloc, et le cache d'images oublie ses échecs."
