#!/usr/bin/env bash
#
# Recette du poste N11 — ne rien perdre en fermant, retrouver ses documents (fiche 23, d19,
# e19, f25).
#
# Nouveau, Ouvrir, Fermer (Ctrl+W, Ctrl+F4), Quitter et la fermeture de la fenêtre jetaient les
# modifications sans un mot. Ils demandent désormais, avant de perdre : Enregistrer, Ne pas
# enregistrer, Annuler. Fermer rend le catalogue des modèles. Les six derniers documents sont
# au menu Fichier, numérotés, gardés dans les préférences de l'utilisateur
# (MyPublisher.Core/Services/Preferences.cs), que A11 et N25 reprendront. Trois temps.
#
# 1. LES SOURCES, lues par un script :
#
#      a. la question n'est branchée que par App.axaml.cs, une fois, et les préférences de
#         l'utilisateur n'y sont chargées que là : nulle part ailleurs dans l'application ni au
#         banc (les tests ferment des fenêtres modifiées) ; le modèle de vue prend, sans elles,
#         un magasin en mémoire, qui n'écrit nulle part ;
#      b. chaque geste qui perd le document passe par LaisserPartirLeDocument — le catalogue
#         une fois le modèle choisi, Ouvrir par OuvrirLeDocument, Fermer, la fermeture de la
#         fenêtre (OnClosing), et Quitter, qui ferme la fenêtre — ; elle referme la saisie et le
#         champ avant de lire IsDirty ;
#      c. le dialogue a un bouton par défaut (Enregistrer) et un bouton d'annulation
#         (Annuler), et il est une vue de la phase, inscrite à la recette I1, qui la garde de
#         toute couleur en dur ;
#      d. Ctrl+W et Ctrl+F4 mènent à Fermer, sont au tableau des raccourcis du README, et
#         OnKeyDown ne prend pas W ;
#      e. ce que le poste ajoute au modèle de vue est dans son partiel (règle 10), et le
#         magasin des préférences se désigne par la variable MYPUBLISHER_PREFERENCES.
#
# 2. LES TESTS, sous « poste=N11 », la variable MYPUBLISHER_PREFERENCES pointée vers un
#    dossier temporaire, et la configuration de la personne relevée avant et après — elle ne
#    doit pas avoir bougé :
#
#      · le noyau (« PreferencesTests ») : six documents, sans doublon, le plus récent en tête ;
#        relus après un redémarrage ; un fichier illisible rend un magasin vide ; une clé
#        inconnue survit ; un magasin en mémoire n'écrit nulle part ; la variable désigne le
#        dossier ;
#      · la vraie fenêtre (« FermerSansPerdreTests »), la question branchée comme l'application
#        la branche : les trois réponses sur Nouveau, Ouvrir, Fermer et la fenêtre — Enregistrer
#        écrit puis ferme, Ne pas enregistrer ferme, Annuler laisse tout tel quel — ; aucune
#        question sur un document intact ; Ctrl+W, Ctrl+F4 et le menu ; la frappe d'une saisie
#        ouverte compte ; Entrée, Échap et la barre de titre du dialogue ; un enregistrement
#        qui échoue ne perd rien ; les récents survivent à un redémarrage, sans doublon, le plus
#        récent en tête, et se rouvrent d'un clic ; un récent disparu quitte la liste ; sans la
#        question branchée, une fenêtre de test ne demande rien.
#
# 3. LES VOISINS : le catalogue des modèles (B1), où la question s'est glissée après le choix,
#    et MasquerEcranTests, qui ferme des fenêtres modifiées.
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

# ---------------------------------------------------------------------- 1. les sources

python3 - "$racine" <<'PY'
import pathlib, re, sys

racine = pathlib.Path(sys.argv[1])
VUES = racine / "MyPublisher/Views"

echecs = []
def exige(condition, message):
    if not condition:
        echecs.append(message)

def lire(chemin):
    p = racine / chemin
    return p.read_text(encoding="utf-8") if p.exists() else ""

def corps(source, signature):
    """Le corps d'une méthode, de son accolade ouvrante à la fermante de même retrait."""
    trouve = re.search(re.escape(signature) + r"\s*\n(\s*)\{\n(.*?)\n\1\}", source, re.S)
    return trouve.group(2) if trouve else None

app = lire("MyPublisher/App.axaml.cs")
fenetre = lire("MyPublisher/Views/MainWindow.axaml.cs")
fermer = lire("MyPublisher/Views/MainWindow.Fermer.cs")
axaml = lire("MyPublisher/Views/MainWindow.axaml")
question = lire("MyPublisher/Views/QuestionAvantDePerdre.axaml")
partiel = lire("MyPublisher/ViewModels/MainWindowViewModel.Fermer.cs")
modele = lire("MyPublisher/ViewModels/MainWindowViewModel.cs")
preferences = lire("MyPublisher.Core/Services/Preferences.cs")

# ------------------------------------------- a. la question, branchée par l'application seule

branchement = "fenetre.AvantDePerdre = nom => QuestionAvantDePerdre.Poser(fenetre, nom);"
exige(app.count(branchement) == 1, "App.axaml.cs ne branche pas la question, une fois, sur la fenêtre qu'il crée.")
exige(app.count("new MainWindowViewModel(preferences: Preferences.Charger());") == 1,
      "App.axaml.cs ne charge pas les préférences de l'utilisateur pour le modèle de vue.")
for chemin in sorted(list((racine / "MyPublisher").rglob("*.cs")) + list((racine / "MyPublisher.Banc").rglob("*.cs"))):
    if {"bin", "obj"} & set(chemin.relative_to(racine).parts) or chemin.name == "App.axaml.cs":
        continue
    texte = chemin.read_text(encoding="utf-8")
    nom = chemin.relative_to(racine)
    exige(not re.search(r"\bAvantDePerdre\s*=(?!=)", texte), f"{nom} branche la question : seul App.axaml.cs le fait.")
    exige("Preferences.Charger(" not in texte, f"{nom} charge des préférences : seul App.axaml.cs charge celles de l'utilisateur.")
exige("Preferences = preferences ?? Preferences.EnMemoire();" in modele,
      "le modèle de vue construit sans préférences n'en prend pas un magasin en mémoire.")
print("    a. la question et les préférences de l'utilisateur, branchées par App.axaml.cs seul")

# ------------------------------------------------- b. chaque geste passe par la question

laisser = corps(fermer, "public async Task<bool> LaisserPartirLeDocument()") or ""
exige(laisser != "", "LaisserPartirLeDocument est introuvable dans MainWindow.Fermer.cs.")
lecture = laisser.find("vm.IsDirty")
for refermer in ("PageView.EndTextEdit();", "ClosePropertyEdit();"):
    exige(0 <= laisser.find(refermer) < lecture, f"LaisserPartirLeDocument ne fait pas « {refermer} » avant de lire IsDirty.")
for reponse in ("NePasEnregistrer", "Enregistrer"):
    exige(f"case ReponseAvantDePerdre.{reponse}:" in laisser, f"LaisserPartirLeDocument ne traite pas la réponse « {reponse} ».")
exige("if (!vm.IsDirty) return true;" in laisser,
      "Enregistrer laisse poursuivre sans vérifier que l'écriture a eu lieu.")

choisir = corps(fenetre, "private async Task ChooseTemplateAsync()") or ""
exige(re.search(r"ShowDialog<TemplateChoice\?>\(this\) is not \{ \} choice\) return;\s*\n\s*if \(!await LaisserPartirLeDocument\(\)\) return;", choisir),
      "le catalogue ne pose pas la question une fois le modèle choisi, avant de l'appliquer.")
ouvrir = corps(fenetre, "private async void OnOpen(object? sender, RoutedEventArgs e)") or ""
exige("await OuvrirLeDocument(path);" in ouvrir and "LoadFrom" not in ouvrir,
      "Ouvrir… ne passe pas par OuvrirLeDocument, qui pose la question.")
for signature in ("public async Task<bool> OuvrirLeDocument(string chemin)", "public async Task FermerLeDocument()"):
    exige("if (!await LaisserPartirLeDocument()) return" in (corps(fermer, signature) or ""),
          f"« {signature} » ne pose pas la question avant d'agir.")
exige("private void OnQuit(object? sender, RoutedEventArgs e) => Close();" in fenetre,
      "Quitter ne ferme plus la fenêtre, où la question est posée.")
fermeture = corps(fermer, "protected override void OnClosing(WindowClosingEventArgs e)") or ""
exige("e.Cancel = true;" in fermeture and "FermerApresLaQuestion()" in fermeture and "base.OnClosing(e);" in fermeture,
      "la fermeture de la fenêtre n'est pas suspendue le temps de la question.")
exige("_vm.FermetureDemandee += OnFermetureDemandee;" in fenetre and "_vm.FermetureDemandee -= OnFermetureDemandee;" in fenetre,
      "la fenêtre ne s'abonne pas à Fermer du modèle de vue qu'elle prend, ou ne s'en désabonne pas.")
print("    b. Nouveau, Ouvrir, Fermer, Quitter et la fenêtre passent par la question, la saisie refermée d'abord")

# --------------------------------------------------------------- c. le dialogue

exige(re.search(r'x:Name="BoutonEnregistrer"[^>]*IsDefault="True"', question, re.S),
      "Enregistrer n'est pas le bouton par défaut du dialogue.")
exige(re.search(r'x:Name="BoutonAnnuler"[^>]*IsCancel="True"', question, re.S),
      "Annuler n'est pas le bouton d'annulation du dialogue.")
exige(len(re.findall(r"IsDefault=", question)) == 1 and len(re.findall(r"IsCancel=", question)) == 1,
      "le dialogue a plus d'un bouton par défaut ou d'annulation.")
apparence = lire("Scripts/recettes/I1-apparence.sh")
liste = re.search(r"vues_de_la_phase=\((.*?)\)", apparence, re.S)
exige(liste is not None and "MyPublisher/Views/QuestionAvantDePerdre.axaml" in liste.group(1).split(),
      "le dialogue n'est pas inscrit dans « vues_de_la_phase » de la recette I1.")
print("    c. le dialogue : un bouton par défaut, un bouton d'annulation, une vue de la phase")

# --------------------------------------------------------------- d. Ctrl+W et Ctrl+F4

for geste in ("Ctrl+W", "Ctrl+F4"):
    exige(axaml.count(f'<KeyBinding Gesture="{geste}" Command="{{Binding FermerLeDocumentCommand}}"/>') == 1,
          f"« {geste} » ne mène pas à Fermer, une fois.")
exige('Command="{Binding FermerLeDocumentCommand}"' in axaml.split("</Window.KeyBindings>")[1],
      "le menu Fichier n'a pas d'entrée Fermer.")
cle = corps(fenetre, "protected override void OnKeyDown(KeyEventArgs e)") or ""
exige(not re.search(r"case Key\.W\b", cle) and not re.search(r"case Key\.F4\b", cle),
      "OnKeyDown prend W ou F4 : la liaison ne les verrait jamais (règle 9).")
readme = lire("README.md")
tableau = re.search(r"^## Raccourcis$(.*?)^## ", readme, re.S | re.M)
exige(tableau is not None and "| `Ctrl+W` ou `Ctrl+F4` |" in tableau.group(1),
      "le tableau des raccourcis du README ne dit pas Ctrl+W et Ctrl+F4.")
print("    d. Ctrl+W et Ctrl+F4 mènent à Fermer, au README, et OnKeyDown les laisse")

# ----------------------------------------------- e. le partiel, et le magasin des préférences

exige("public partial class MainWindowViewModel" in partiel,
      "ce que N11 ajoute au modèle de vue n'est pas dans son partiel, MainWindowViewModel.Fermer.cs (règle 10).")
for membre in ("public Preferences Preferences { get; }", "private void FermerLeDocument()", "public event EventHandler? FermetureDemandee;",
               "public IReadOnlyList<string> DocumentsRecents"):
    exige(membre in partiel, f"« {membre} » n'est pas dans le partiel du poste.")
    exige(membre not in modele, f"« {membre} » est dans MainWindowViewModel.cs, hors du partiel.")
exige('public const string VariableDuDossier = "MYPUBLISHER_PREFERENCES";' in preferences,
      "le dossier des préférences ne se désigne pas par MYPUBLISHER_PREFERENCES.")
exige("public const int NombreDeRecents = 6;" in preferences, "le magasin ne garde pas six documents récents.")
print("    e. le partiel du poste ; le magasin des préférences, désigné par MYPUBLISHER_PREFERENCES")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : la question branchée par l'application seule, chaque geste qui perd passe par elle, le dialogue, Ctrl+W et Ctrl+F4, le partiel"

# ------------------------------------------------------------------------ 2. les tests

# Les préférences des tests dans un dossier à la recette ; la configuration de la personne,
# relevée avant et après — elle ne doit pas avoir bougé.
export MYPUBLISHER_PREFERENCES="$travail/preferences"
configuration="${XDG_CONFIG_HOME:-$HOME/.config}/MyPublisher/preferences.json"
releve() { if [ -e "$configuration" ]; then stat -c '%Y %s' "$configuration"; else echo absente; fi; }
avant="$(releve)"

jouer() {
    local projet="$1" filtre="$2" attendus="$3" quoi="$4"
    if ! dotnet test "$projet" ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
            --nologo -v q --filter "$filtre" > "$travail/tests.txt" 2>&1; then
        sed 's/^/    /' "$travail/tests.txt" >&2
        echo "ÉCHEC : les tests sous « $filtre » ne passent pas." >&2
        exit 1
    fi
    joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
    if [ "${joues:-0}" -lt "$attendus" ]; then
        echo "ÉCHEC : $attendus tests attendus sous « $filtre », ${joues:-0} joués." >&2
        echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
        exit 1
    fi
    echo "  $quoi ($joues tests)"
}

jouer MyPublisher.Tests/MyPublisher.Tests.csproj "poste=N11" 7 \
    "2. le noyau : six récents sans doublon, relus, un fichier illisible, une clé inconnue, le magasin en mémoire, la variable"
jouer MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj "poste=N11" 29 \
    "   la vraie fenêtre : les trois réponses sur Nouveau, Ouvrir, Fermer et la fenêtre, le document intact, la saisie ouverte, le dialogue, les récents"

apres="$(releve)"
if [ "$avant" != "$apres" ]; then
    echo "ÉCHEC : les tests ont touché la configuration de la personne ($configuration : $avant, puis $apres)." >&2
    exit 1
fi
echo "     la configuration de la personne n'a pas bougé ($configuration : $apres)"

# ------------------------------------------------------------------------ 3. les voisins

jouer MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj "poste=B1" 5 \
    "3. les voisins : le catalogue des modèles, la question glissée après le choix (B1)"
jouer MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj "FullyQualifiedName~MasquerEcranTests" 17 \
    "   les voisins : des fenêtres modifiées qui se ferment sans rien demander (I8)"

echo "N11 : avant de perdre des modifications, Nouveau, Ouvrir, Fermer, Quitter et la fenêtre demandent ; les six derniers documents sont au menu Fichier, et survivent à un redémarrage."
