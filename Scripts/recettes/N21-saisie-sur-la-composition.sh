#!/usr/bin/env bash
#
# Recette du poste N21 — la saisie montre le texte composé, voie 2a (fiche 23, a10, a11, a20 et
# a21 ; la voie choisie par le propriétaire le 26 octobre 2026 sur la mesure de N7).
#
# Pendant la saisie, l'écran montrait un champ en texte brut à la place de la composition : ni
# fragments, ni écarts de paragraphe, ni taquets, ni colonnes, une autre coupure — jusqu'à
# 471 pt entre son curseur et le caractère composé. La composition reste désormais à l'écran ;
# le champ, caché et hors du test de survol, ne fait plus que la frappe, la méthode d'entrée, le
# presse-papiers et la navigation au clavier ; le curseur, la sélection et le pré-édit se
# dessinent sur la composition, dans l'Overlay ; le clic, le glissé, le double et le triple clic
# se lisent sur elle. Deux temps.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. la planche : la composition n'est plus masquée pendant la saisie ; le champ est caché
#         (opacité nulle) et hors du test de survol, sa liaison « Saisie » gardée ; les marques
#         de la saisie vivent dans l'Overlay, hors de PageRoot, habillées par l'apparence ;
#      b. la mécanique vit dans PageCanvas.Saisie.cs, et non dans le fichier principal de la
#         planche (règle 16) : la couche ne laisse plus rien au champ, la pression dans le texte
#         en saisie est prise au passage descendant, le glissé se lit sur la composition et non
#         plus dans la mise en page du champ ;
#      c. le noyau : le bord d'un caractère se lit dans le morceau façonné d'un tenant
#         (IMetriques.Bord, par défaut la chasse du préfixe, celle du PDF qui ne façonne pas),
#         qu'Abscisse demande et que MetriquesAvalonia lit dans la mise en page d'Avalonia —
#         la reprise de N3 que la décision demandait avant la voie 2a ;
#      d. la vue neuve est à la recette I1 ; le README dit le triple-clic.
#
# 2. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre (trait
#    « poste=N21 » : SaisieSurLaCompositionTests) — la recette du plan, et ce qui l'entoure :
#
#      · pendant la saisie, l'encre de la page égale celle d'avant l'entrée, au pixel, sur
#        l'exemple et ATA 21 ; ce qu'on tape se voit tel que la page le montre hors saisie ;
#      · un clic sur un caractère de chaque bloc d'ATA 21 qu'un clic atteint y pose le curseur,
#        dessiné sous lui à moins du seuil de N7 (0,75 pt), à l'entrée comme en saisie ;
#      · chaque bord de caractère de l'exemple et d'ATA 21 est au dessin à 0,01 pt, ligatures et
#        crénages compris, et le clic retrouve chaque position ;
#      · le glissé en saisie suit la composition (retrait de paragraphe, fragment d'un autre
#        corps) ; le double-clic prend le mot, le triple-clic le paragraphe, et le glissé qui
#        suit un double-clic étend par mots ; Maj+clic étend, le clic droit garde la sélection ;
#      · la sélection dessinée reste celle du modèle de vue quand la barre prend le focus (N15) ;
#        le curseur se dessine sur un bloc tourné à 200 % ; le pré-édit de la méthode d'entrée se
#        dessine au curseur.
#
#    Le compte des tests joués est vérifié : un filtre qui ne désigne plus rien sort en 0.
#
# Hors du poste, et dit par lui : la navigation au clavier sur les lignes composées (Début, Fin,
# haut, bas, page) reste au champ caché, un poste à part que le plan ouvre à la décision du
# propriétaire. Le PDF ne change pas : Scripts/comparer-rendus.sh le vérifie.
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
    trouve = re.search(re.escape(signature) + r"(.*?)\n    }\n", texte, re.S)
    return trouve.group(1) if trouve else ""

planche_axaml = lire("MyPublisher/Views/PageCanvas.axaml")
planche = lire("MyPublisher/Views/PageCanvas.axaml.cs")
saisie = lire("MyPublisher/Views/PageCanvas.Saisie.cs")
marques_chemin = racine / "MyPublisher/Views/MarquesDuTexte.cs"
marques = marques_chemin.read_text(encoding="utf-8") if marques_chemin.exists() else ""
compositeur = lire("MyPublisher.Core/Models/Compositeur.cs")
controle = lire("MyPublisher/Views/ControleDeBloc.cs")

# a. la planche.
exige(planche_axaml.count('<views:TexteCompose Bloc="{Binding}">') == 1
      and not re.search(r"<views:TexteCompose[^>]*IsEditing", planche_axaml),
      "la composition est encore masquée pendant la saisie.")
champ = re.search(r'<TextBox IsVisible="\{Binding IsEditing\}"(.*?)>', planche_axaml, re.S)
exige(champ is not None, "le champ de la saisie n'est plus dans le gabarit du bloc de texte.")
if champ:
    exige('Opacity="0"' in champ.group(1) and 'IsHitTestVisible="False"' in champ.group(1),
          "le champ de la saisie n'est pas caché et hors du test de survol.")
    exige('Text="{Binding Saisie, Mode=TwoWay}"' in champ.group(1), "le champ n'est plus lié à « Saisie » (Q1).")
overlay = re.search(r'<Canvas Name="Overlay" IsHitTestVisible="False">(.*?)</Canvas>', planche_axaml, re.S)
exige(overlay is not None and '<views:MarquesDuTexte Name="MarquesDeLaSaisie"' in overlay.group(1),
      "les marques de la saisie ne sont pas dans l'Overlay.")
racine_page = re.search(r'<Panel Name="PageRoot"(.*?)</LayoutTransformControl>', planche_axaml, re.S)
exige(racine_page is not None and "MarquesDuTexte" not in racine_page.group(1),
      "les marques de la saisie sont dans PageRoot, que l'écran compare au PDF.")
style = re.search(r'<Style Selector="views\|MarquesDuTexte">(.*?)</Style>', planche_axaml, re.S)
exige(style is not None and all(f'<Setter Property="{p}" Value="{{StaticResource ' in style.group(1)
                                for p in ("Curseur", "Selection", "FondDuPreEdit")),
      "les marques de la saisie ne prennent pas leurs couleurs à l'apparence.")
exige(re.search(r"#[0-9A-Fa-f]{6}|Colors\.|Brushes\.", marques) is None,
      "MarquesDuTexte écrit une couleur en dur.")
print("    a. la composition reste ; le champ est caché, hors du test de survol ; les marques dans l'Overlay")

# b. la mécanique, dans le partiel de la saisie.
for nom in ("PressionDansLaSaisie", "MarquesDeLaSaisie", "PoserLesMarquesDuTexte", "InputLayer.Laisse = null"):
    exige(nom not in planche, f"le fichier principal de la planche porte « {nom} » (règle 16 : PageCanvas.Saisie.cs).")
attache = corps(saisie, "protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)")
exige("InputLayer.Laisse = null;" in attache, "la couche laisse encore le texte en saisie au champ caché.")
exige("AddHandler(PointerPressedEvent, PressionDansLaSaisie, RoutingStrategies.Tunnel);" in attache,
      "la pression dans le texte en saisie n'est pas prise au passage descendant, avant OnPointerPressed.")
etendre = corps(saisie, "private void EtendreLaSelectionDuTexte(PointerEventArgs e)")
exige(etendre != "" and "geometrie.IndexSous(point)" in etendre and "HitTestPoint" not in saisie,
      "le glissé dans le texte se lit encore dans la mise en page du champ, et non sur la composition.")
pression = corps(saisie, "private void PressionDansLaSaisie(object? sender, PointerPressedEventArgs e)")
exige("e.ClickCount" in pression and "ParagrapheDe(texte, caractere)" in pression and "MotDe(texte, caractere)" in pression,
      "le double et le triple clic ne prennent pas le mot et le paragraphe.")
poser = corps(saisie, "private void PoserLesMarquesDuTexte()")
exige("_vm?.SelectionDuTexte" in poser and "compose.TransformToVisual(marques)" in poser,
      "les marques ne dessinent pas la sélection du modèle de vue (N15) sur la composition.")
print("    b. PageCanvas.Saisie.cs : la couche garde le texte en saisie, la souris lue sur la composition")

# c. le noyau.
interface = re.search(r"public interface IMetriques(.*?)\n    }\n", compositeur, re.S)
exige(interface is not None and re.search(
    r"double Bord\(string texte, StyleEffectif style, int position\) =>\s*position <= 0 \? 0 : Largeur\(texte\[\.\.position\], style\);",
    interface.group(1)) is not None,
      "IMetriques n'a pas de Bord, la chasse du préfixe par défaut.")
abscisse = corps(compositeur, "public static double Abscisse(")
exige("metriques.Bord(morceau.Texte, morceau.Style, dedans)" in abscisse and "metriques.Largeur(morceau.Texte[..dedans]" not in abscisse,
      "Abscisse mesure encore le préfixe seul, hors du morceau façonné.")
exige("public double Bord(string texte, StyleEffectif style, int position)" in controle and "HitTestTextPosition" in controle,
      "MetriquesAvalonia ne lit pas le bord dans le morceau façonné.")
print("    c. le bord d'un caractère est lu dans le morceau façonné ; le PDF garde la chasse du préfixe")

# d. la recette I1 et le README.
recette_i1 = lire("Scripts/recettes/I1-apparence.sh")
liste = re.search(r"vues_de_la_phase=\((.*?)\)", recette_i1, re.S).group(1).split()
exige("MyPublisher/Views/MarquesDuTexte.cs" in liste, "MarquesDuTexte.cs n'est pas dans la liste des vues de la phase de la recette I1.")
readme = lire("README.md")
exige(re.search(r"^\| Triple-clic \| Dans le texte en saisie, sélectionne le paragraphe", readme, re.M) is not None,
      "le README ne dit pas le triple-clic.")
print("    d. la vue neuve est à la recette I1 ; le README dit le triple-clic")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : la planche, la saisie, le noyau, la recette I1, le README"

# ------------------------------------------------------------------------ 2. les gestes

attendus=10
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo --filter "poste=N21" > "$travail/tests.txt" 2>&1; then
    grep -E "Failed |Error Message|Assert" -A3 "$travail/tests.txt" | sed 's/^/    /' >&2 || sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : la saisie sur la composition (« poste=N21 ») ne passe pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=N21 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  2. les gestes sur la vraie fenêtre : $joues tests (l'encre au pixel, le clic sur ATA 21 sous le seuil de N7, les bords façonnés, le glissé, le mot, le paragraphe, Maj+clic, la barre, le bloc tourné, le pré-édit)"

echo "N21 : pendant la saisie, la page montre le texte tel qu'il s'imprime ; le curseur, la sélection et le pré-édit se dessinent dessus, et la souris vise les caractères qu'on voit."
