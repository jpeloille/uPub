#!/usr/bin/env bash
#
# Recette du poste N16 — tabulation et saut de ligne forcé (fiche 23, a15 et a16).
#
# Tab n'était pas inséré — AcceptsTab n'était pas posé — : il emmenait le focus au contrôle
# suivant et laissait la saisie ouverte, orpheline. Entrée, Maj+Entrée et Ctrl+Entrée
# inséraient toutes un saut de paragraphe, et le champ écrivait Environment.NewLine, « \r\n »
# sous Windows. Publisher distingue la fin de paragraphe du saut de ligne forcé (Maj+Entrée).
# Trois temps.
#
# 1. LES SOURCES, lues comme on les livre :
#
#      a. le champ de la page accepte la tabulation et écrit « \n » ; la cible du juge
#         « Text="{Binding Saisie, Mode=TwoWay}" » y reste, une fois (piège du plan) ; le champ
#         Contenu du dock écrit « \n » et n'accepte pas la tabulation : Tab y reste la
#         navigation ;
#      b. le saut forcé est U+2028 (Compositeur.SautDeLigne), que Decouper coupe sans fermer le
#         paragraphe, et que Recalage ne compte pas comme un saut ;
#      c. la planche pose U+2028 sous Maj+Entrée et garde Maj+Tab — le champ, qui accepte la
#         tabulation, en écrirait une —, depuis PageCanvas.Saisie.cs, et non depuis son fichier
#         principal (règle 16) ;
#      d. le README dit Entrée, Maj+Entrée et Tab dans la saisie.
#
# 2. LE NOYAU, par le trait « poste=N16 » de « MyPublisher.Tests » (SautDeLigneForceTests) :
#    le saut forcé coupe la ligne sans fermer le paragraphe, ni retrait de première ligne ni
#    espace de paragraphe après lui, les écarts restent à leur paragraphe, les blancs avant lui
#    sont rognés, le curseur à droite de la ligne se pose avant lui, et le taper ne décale aucun
#    écart.
#
# 3. LES GESTES, joués par « MyPublisher.Tests.Interface » sur la vraie fenêtre (trait
#    « poste=N16 » : TabulationEtSautForceTests) — la recette du plan :
#
#      · Tab dans le 2ᵉ paragraphe met « \t » au curseur ; le mot suivant tombe au taquet de
#        36 pt, à l'écran (la composition de la planche) comme au PDF (pdftotext -bbox), à
#        0,05 pt près ; la saisie et le focus restent dans le champ ; la frappe fait un pas ;
#      · Maj+Tab ne sort pas du bloc ; dans le dock, Tab va au champ suivant ;
#      · Maj+Entrée coupe la ligne sans créer de paragraphe : le retrait de première ligne ne
#        s'y applique pas, le compte des paragraphes ne change pas, l'écart du paragraphe
#        suivant reste le sien ; la frappe fait un pas ;
#      · Maj+Entrée sur une sélection la remplace, le curseur après le saut ;
#      · Entrée crée un paragraphe, et les deux champs écrivent « \n ».
#
#    Les comptes des tests joués sont vérifiés : un filtre qui ne désigne plus rien sort en 0.
#
# La composition des documents existants ne change pas : aucun ne porte de U+2028. Le poste
# l'a mesuré par l'outil d'empreinte de N3 (Scripts/empreinte-composition.sh), avant et après,
# sur les exemples, les modèles, ATA 21 et les Fiches : identique. La recette ne le rejoue
# pas — il demande un relevé de référence que le dépôt ne garde pas.
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

# a. les deux champs.
page = lire("MyPublisher/Views/PageCanvas.axaml")
champ = re.search(r'<TextBox IsVisible="\{Binding IsEditing\}"(.*?)>', page, re.S)
exige(champ is not None, "le champ de la saisie n'est plus dans PageCanvas.axaml.")
if champ:
    exige('AcceptsTab="True"' in champ.group(1), "le champ de la page n'accepte plus la tabulation.")
    exige('NewLine="&#10;"' in champ.group(1), "le champ de la page n'écrit plus « \\n » à Entrée.")
exige(page.count('Text="{Binding Saisie, Mode=TwoWay}"') == 1,
      "la cible du juge « Text=\"{Binding Saisie, Mode=TwoWay}\" » n'est plus là, une fois (piège du plan).")
fenetre = lire("MyPublisher/Views/MainWindow.axaml")
contenu = re.search(r'<TextBox x:Name="SaisieDuTexte"(.*?)/>', fenetre, re.S)
exige(contenu is not None, "le champ Contenu du dock n'est plus dans MainWindow.axaml.")
if contenu:
    exige('NewLine="&#10;"' in contenu.group(1), "le champ Contenu du dock n'écrit plus « \\n » à Entrée.")
    exige("AcceptsTab" not in contenu.group(1), "le champ Contenu du dock accepte la tabulation : Tab y était la navigation.")
print("    a. le champ de la page accepte Tab et écrit « \\n » ; le champ Contenu écrit « \\n », Tab y navigue")

# b. le noyau.
compositeur = lire("MyPublisher.Core/Models/Compositeur.cs")
exige("public const char SautDeLigne = '\\u2028';" in compositeur, "le saut de ligne forcé n'est plus U+2028.")
decouper = re.search(r"private static List<Unite> Decouper\((.*?)\n    }\n", compositeur, re.S)
exige(decouper is not None and "FinDeLigne: false, i, 1, SautForce: true" in decouper.group(1),
      "Decouper ne coupe plus au saut forcé sans fermer le paragraphe.")
exige("Fermer(finDeParagraphe: false, fin: mot.Debut, suivante: mot.Debut + mot.Longueur);" in compositeur,
      "la composition ne coupe plus la ligne au saut forcé dans le paragraphe.")
recalage = lire("MyPublisher.Core/Models/Recalage.cs")
saut = re.search(r"private static bool EstSaut\(string texte, int i\) =>(.*?);", recalage, re.S)
exige(saut is not None and "2028" not in saut.group(1) and "SautDeLigne" not in saut.group(1),
      "Recalage compte le saut forcé comme un saut de paragraphe.")
print("    b. le saut forcé est U+2028 : Decouper coupe la ligne dans le paragraphe, Recalage ne le compte pas")

# c. la planche.
saisie = lire("MyPublisher/Views/PageCanvas.Saisie.cs")
planche = lire("MyPublisher/Views/PageCanvas.axaml.cs")
touche = re.search(r"private void ToucheDeLaSaisie\(object\? sender, KeyEventArgs e\)(.*?)\n    }\n", saisie, re.S)
exige(touche is not None, "PageCanvas.Saisie.cs ne prend plus Maj+Entrée et Maj+Tab au champ.")
if touche:
    exige("case Key.Enter:" in touche.group(1) and "PoserLeSautForce(zone);" in touche.group(1),
          "Maj+Entrée ne pose plus le saut forcé.")
    exige("case Key.Tab:" in touche.group(1), "Maj+Tab n'est plus gardé dans le bloc.")
poser = re.search(r"private static void PoserLeSautForce\(TextBox zone\)(.*?)\n    }\n", saisie, re.S)
exige(poser is not None and "Compositeur.SautDeLigne" in poser.group(1),
      "le saut forcé posé n'est plus U+2028 (Compositeur.SautDeLigne).")
exige("AddHandler(KeyDownEvent, ToucheDeLaSaisie, RoutingStrategies.Tunnel);" in saisie,
      "la planche n'écoute plus les touches de la saisie avant le champ.")
exige("ToucheDeLaSaisie" not in planche, "le fichier principal de la planche prend les touches de la saisie (règle 16).")
print("    c. Maj+Entrée et Maj+Tab sont pris au champ depuis PageCanvas.Saisie.cs")

# d. le README.
readme = lire("README.md")
exige(re.search(r"^\| Dans la saisie d'un bloc : `Entrée` / `Maj\+Entrée` / `Tab` \|", readme, re.M) is not None,
      "le README ne dit plus Entrée, Maj+Entrée et Tab dans la saisie.")
print("    d. le README dit Entrée, Maj+Entrée et Tab dans la saisie")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : les deux champs, le noyau, la planche, le README"

# ------------------------------------------------------------------------ 2 et 3. les tests

jouer() {
    local projet="$1" attendus="$2" quoi="$3"
    local sortie
    sortie="$travail/$(basename "$projet").txt"
    if ! dotnet test "$projet" ${MYPUBLISHER_DEJA_COMPILE:+--no-build} --nologo --filter "poste=N16" > "$sortie" 2>&1; then
        grep -E "Failed |Error Message|Assert" -A3 "$sortie" | sed 's/^/    /' >&2 || sed 's/^/    /' "$sortie" >&2
        echo "ÉCHEC : $quoi (« poste=N16 ») ne passe pas." >&2
        exit 1
    fi
    joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$sortie" | head -1)"
    if [ "${joues:-0}" -lt "$attendus" ]; then
        echo "ÉCHEC : $attendus tests attendus sous « poste=N16 » dans $projet, ${joues:-0} joués." >&2
        echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
        exit 1
    fi
}

jouer MyPublisher.Tests/MyPublisher.Tests.csproj 8 "le saut forcé dans le noyau"
echo "  2. le noyau : $joues tests (la ligne coupée dans le paragraphe, le retrait, les écarts, le curseur, la frappe)"

jouer MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj 6 "la tabulation et le saut forcé sur la fenêtre"
echo "  3. les gestes sur la vraie fenêtre : $joues tests (Tab au taquet, à l'écran et au PDF ; Maj+Tab ; le dock ; Maj+Entrée, sur une sélection aussi ; Entrée)"

echo "N16 : Tab met une tabulation au taquet sans quitter le bloc ; Maj+Entrée coupe la ligne sans créer de paragraphe ; Entrée écrit « \\n »."
