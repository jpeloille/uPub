#!/usr/bin/env bash
#
# Recette du poste N2 — mettre en forme une plage, dans le noyau. Deux temps, tout en
# fonctions pures : aucune vue ne change, et rien de l'application n'appelle encore
# « MiseEnForme » — R6 et R7 le feront.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. « MiseEnForme » est dans le noyau et n'y dépend de rien d'Avalonia ; il porte
#         « Lire », « Poser », « Basculer », « Retirer » sur les fragments ET sur les écarts
#         de paragraphe, « Allume » et « Paragraphes » ;
#      b. l'écriture passe par les opérations de la collection (Q17) : « MiseEnForme » ne
#         remplace aucune collection, et un bout coupé est une copie (« Clone ») — jamais le
#         fragment d'origine retouché sur place, que « ControleDeBloc » ne verrait pas ;
#      c. un fragment et un écart sont vus au même endroit par la composition et par la
#         mise en forme : « Decouper » et « Resoudre » passent par l'« Etendue » que
#         « MiseEnForme » emploie, et non par leur propre calcul des bornes ;
#      d. « Recalage.ParagrapheDe » et « Recalage.NombreDeParagraphes » sont publics, et
#         comptent par « Sauts » : une seule façon de compter les paragraphes.
#
# 2. LES TESTS, par leur trait « poste=N2 » (« MiseEnFormeTests ») : les quatre cas de la
#    recette de PLAN.md, chiffre pour chiffre — le gras sur [4, 7) de « abc def » donne
#    {4, 3, Bold}, rebasculé il ne laisse rien ; une plage à cheval sur deux fragments
#    importés qui se recouvrent rend les mêmes styles hors d'elle ; un gras retiré sur un
#    bloc gras devient « Bold = false » ; le centrage du deuxième paragraphe, puis du
#    troisième, fusionne en {1, 2, Center} — ; les bornes (plage vide, au bout du texte,
#    sur tout le texte, fragment à cheval, recouvrements, valeur du bloc, couleur du jeu,
#    décalage de ligne de base, souligné et ses sortes, paragraphes vides) ; l'écriture par
#    la collection, sans rien modifier sur place ni rien annoncer sans changement réel ;
#    l'exhaustivité des propriétés, par réflexion ; deux balayages de deux mille poses au
#    hasard, où hors de la plage rien ne change à l'œil et où la pose rejouée ne fait rien.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# ------------------------------------------------------------------------ 1. les sources

python3 - "$racine" <<'PY'
import pathlib, re, sys

racine = pathlib.Path(sys.argv[1])
echecs = []

def exige(condition, message):
    if not condition:
        echecs.append(message)

modeles = racine / "MyPublisher.Core/Models"
mise_en_forme = (modeles / "MiseEnForme.cs").read_text(encoding="utf-8")

# a. dans le noyau, pur, et complet.
exige("public static class MiseEnForme" in mise_en_forme, "« MiseEnForme » n'est pas une classe statique du noyau.")
exige(not re.search(r"\busing Avalonia|\bAvalonia\.", mise_en_forme), "« MiseEnForme » dépend d'Avalonia.")
for signature in (
    r"public static StyleDeLaPlage Lire\(\s*string texte, IReadOnlyList<StyleSpan> fragments, StyleEffectif commun, Plage plage, Charte charte\)",
    r"public static bool\? Allume\(\s*string texte, IReadOnlyList<StyleSpan> fragments, StyleEffectif commun, Plage plage, Bascule bascule\)",
    r"public static bool Poser\(\s*string texte, IList<StyleSpan> fragments, StyleEffectif commun, Plage plage, StyleDeLaPlage style\)",
    r"public static bool Basculer\(\s*string texte, IList<StyleSpan> fragments, StyleEffectif commun, Plage plage, Bascule bascule\)",
    r"public static bool Retirer\(string texte, IList<StyleSpan> fragments, StyleEffectif commun, Plage plage\)",
    r"public static Plage Paragraphes\(string texte, Plage caracteres\)",
    r"public static ReglagesDeLaPlage Lire\(\s*string texte, IReadOnlyList<ParagraphSpan> ecarts, ParagrapheEffectif commun, Plage paragraphes\)",
    r"public static bool Poser\(\s*string texte, IList<ParagraphSpan> ecarts, ParagrapheEffectif commun, Plage paragraphes,\s*ReglagesDeLaPlage reglages\)",
    r"public static bool Basculer\(\s*string texte, IList<ParagraphSpan> ecarts, ParagrapheEffectif commun, Plage paragraphes, TextAlign fer\)",
    r"public static bool Retirer\(\s*string texte, IList<ParagraphSpan> ecarts, ParagrapheEffectif commun, Plage paragraphes\)",
):
    exige(re.search(signature, mise_en_forme) is not None, f"« {signature.split('(')[0].replace(chr(92), '')} » n'a plus sa signature.")
print("    a. MiseEnForme : dans le noyau, sans Avalonia, Lire / Poser / Basculer / Retirer sur fragments et écarts")

# b. l'écriture par la collection.
exige(not re.search(r"\.(Spans|Paragraphs)\s*=(?!=)", mise_en_forme), "« MiseEnForme » remplace une collection (Q17).")
exige(not re.search(r"\b(collection|fragments|ecarts)\s*=\s*new\b", mise_en_forme), "« MiseEnForme » remplace la collection qu'on lui donne.")
morceaux = re.findall(r"protected override (?:StyleSpan|ParagraphSpan) Morceau\([^)]*\)\s*\{\s*var morceau = modele\.Clone\(\);", mise_en_forme)
exige(len(morceaux) == 2, "un bout coupé n'est plus une copie de son fragment : il serait retouché sur place.")
print("    b. l'écriture passe par la collection ; un bout coupé est une copie")

# c. une seule étendue.
style = (modeles / "StyleSpan.cs").read_text(encoding="utf-8")
paragraphe = (modeles / "ParagraphSpan.cs").read_text(encoding="utf-8")
exige("var (debut, fin) = Etendue(fragment, texte.Length);" in style, "« Decouper » ne passe plus par « StyleEffectif.Etendue ».")
exige("var (debut, fin) = Etendue(ecart, reglages.Length);" in paragraphe, "« Resoudre » ne passe plus par « ParagrapheEffectif.Etendue ».")
exige("StyleEffectif.Etendue(fragment, longueur)" in mise_en_forme and "ParagrapheEffectif.Etendue(ecart, nombre)" in mise_en_forme,
      "« MiseEnForme » ne voit plus les éléments là où la composition les voit.")
exige(style.count("fragment.Start + fragment.Length") == 1 and "(long)fragment.Start + fragment.Length" in style,
      "la fin d'un fragment se calcule ailleurs que dans « Etendue », ou en entier.")
print("    c. Decouper, Resoudre et MiseEnForme voient chaque élément au même endroit")

# d. un seul compte des paragraphes.
recalage = (modeles / "Recalage.cs").read_text(encoding="utf-8")
exige("public static int ParagrapheDe(string texte, int position) => Sauts(texte, 0, position);" in recalage,
      "« Recalage.ParagrapheDe » n'est plus public, ou ne compte plus par « Sauts ».")
exige("public static int NombreDeParagraphes(string texte) => Sauts(texte, 0, texte.Length) + 1;" in recalage,
      "« Recalage.NombreDeParagraphes » n'est plus public, ou ne compte plus par « Sauts ».")
exige(not re.search(r"Split\(|IndexOf\('\\n'|'\\r'", mise_en_forme), "« MiseEnForme » compte les paragraphes à sa façon.")
print("    d. Recalage.ParagrapheDe et NombreDeParagraphes, publics, par « Sauts »")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : le noyau pur, l'écriture par la collection, une étendue, un compte des paragraphes"

# ------------------------------------------------------------------------ 2. les tests

attendus=26
if ! dotnet test MyPublisher.Tests/MyPublisher.Tests.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} --nologo -v q \
        --filter "poste=N2" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les tests du noyau (« poste=N2 ») ne passent pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=N2 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  2. les tests du poste : $joues tests purs"

echo "N2 : une plage se met en forme dans le noyau — lue, posée, basculée, rendue au bloc —, par la collection, sans rien changer hors d'elle."
