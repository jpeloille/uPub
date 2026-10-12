#!/usr/bin/env bash
#
# Recette du poste C5b — jeu de couleurs et jeu de polices.
#
# L'énoncé : « un document composé avec un jeu ; on change de jeu, puis on exporte. Les
# couleurs relevées au PDF et à l'écran sont celles du nouveau jeu, sur les blocs qui y
# font référence, et seulement sur eux. »
#
# Le document est « Samples/recette-jeux.mypub », composé avec le jeu de couleurs
# « Ardoise » et le jeu de polices « Sobre ». Il porte une référence au jeu partout où
# une couleur se pose — aplat, contour, dégradé, teinte, opacité propre, texte, fragment
# de style, case de tableau, fond de page — et, À CÔTÉ de chacune, un bloc LIBRE qui
# porte en hexadécimal exactement la couleur qu'Ardoise donne à la référence voisine.
# Avant le changement, les deux sont indiscernables : seule la référence les sépare.
# C'est ce qui donne un sens à « et seulement sur eux » — une recette qui ne poserait
# que des blocs du jeu ne verrait pas un changement de jeu qui recolorerait tout.
#
# Le geste « changer de jeu » est joué par le banc, à travers le modèle de vue, comme
# la liste du panneau le joue : « --jeu-de-couleurs Océan --jeu-de-polices Édition ».
# Le banc rend l'écran après le geste, puis enregistre le document ; l'application
# exporte ensuite ce document-là. L'écran et le PDF mesurés sont donc ceux d'un même
# document, passé une fois par le fichier.
#
# Ce qu'elle mesure :
#
#   1. le geste ne touche à AUCUN bloc : le document enregistré ne diffère du document
#      d'origine que par ses deux jeux ;
#   2. avant le changement, au PDF et à l'écran, chaque point porte la couleur d'Ardoise
#      — sans quoi la suite prouverait un document déjà recoloré ;
#   3. après, au PDF et à l'écran, chaque point de référence porte la couleur d'Océan,
#      calculée ici depuis les valeurs du jeu — mélange de la teinte et opacité
#      comprises — et chaque point libre la même couleur qu'avant ;
#   4. les polices : au PDF, « Liberation Serif » (titre d'Édition) et « Liberation
#      Sans » (corps) n'apparaissent qu'après, « DejaVu Serif » (la police libre) des
#      deux côtés ; à l'écran comme au PDF, l'encre du titre et du corps change, celle
#      de la ligne en police libre reste au pixel près ;
#   5. le filet grossier : l'écran et le PDF du document changé donnent la même image
#      (« Scripts/comparer-rendus.sh »), au seuil relevé ci-dessous.
#
# Le seuil de la comparaison d'ensemble vient d'un relevé : 1,63 % avant, 1,57 % après,
# sur le décalage du contour à l'écran (poste Q4, voir la recette de C1) et le lissage
# du texte. Il est à 2,5 % : un seul bloc de la recette resté dans l'ancien jeu d'un
# seul côté couvre 150 × 90 pt, soit 2,7 % de la page.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"
banc="MyPublisher.Banc/bin/Debug/net10.0/MyPublisher.Banc"
document="Samples/recette-jeux.mypub"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln -c Debug --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

for outil in pdftoppm pdffonts magick python3; do
    if ! command -v "$outil" > /dev/null; then
        echo "ÉCHEC : « $outil » introuvable — la recette ne mesurerait rien." >&2
        exit 1
    fi
done

# ------------------------------------------------------------------ les deux états

# Avant : le document tel qu'il est composé.
"$binaire" "$document" --export "$travail/avant.pdf" > /dev/null 2> "$travail/export-avant.txt"
"$banc" rendre "$document" --sortie "$travail/ecran-avant.png" --sans-reperes > /dev/null

# Le geste, puis l'écran qu'il laisse et le document qu'il enregistre.
"$banc" rendre "$document" --sortie "$travail/ecran-apres.png" --sans-reperes \
        --jeu-de-couleurs "Océan" --jeu-de-polices "Édition" \
        --enregistrer "$travail/apres.mypub" > /dev/null
"$binaire" "$travail/apres.mypub" --export "$travail/apres.pdf" > /dev/null 2> "$travail/export-apres.txt"

for etat in avant apres; do
    if [ -s "$travail/export-$etat.txt" ]; then
        echo "ÉCHEC : l'export ($etat) s'est plaint — une police du jeu manquerait-elle ?" >&2
        cat "$travail/export-$etat.txt" >&2
        exit 1
    fi
    pdftoppm -r 144 -f 1 -l 1 -png -singlefile "$travail/$etat.pdf" "$travail/pdf-$etat"
    for rendu in pdf ecran; do
        magick "$travail/$rendu-$etat.png" -background white -alpha remove -alpha off "$travail/$rendu-$etat.ppm"
    done
    pdffonts "$travail/$etat.pdf" > "$travail/polices-$etat.txt"
done

# ------------------------------------------------------------------- 1 à 4 : mesurer

python3 - "$document" "$travail" <<'PY'
import json, sys

document, travail = sys.argv[1], sys.argv[2]
echec = False
def refuser(message):
    global echec
    print(f"    ÉCHEC : {message}", file=sys.stderr)
    echec = True

# --- 1. le geste ne touche aucun bloc ----------------------------------------------
origine = json.load(open(document, encoding="utf-8"))
change = json.load(open(f"{travail}/apres.mypub", encoding="utf-8"))
if origine["ColorScheme"]["Name"] != "Ardoise" or origine["FontScheme"]["Name"] != "Sobre":
    sys.exit("ÉCHEC : le document de recette n'est plus composé avec Ardoise et Sobre.")
if change["ColorScheme"]["Name"] != "Océan" or change["FontScheme"]["Name"] != "Édition":
    sys.exit("ÉCHEC : le geste n'a pas changé de jeu — le document enregistré porte "
             f"« {change['ColorScheme']['Name']} » et « {change['FontScheme']['Name']} ».")
for cle in ("ColorScheme", "FontScheme"):
    del origine[cle], change[cle]
# Les dimensions de la page reviennent du panneau à 1e-12 près : ses deux champs en
# millimètres passent la valeur par un « decimal », et la renvoient au document dès
# que la fenêtre s'ouvre (595,28 devient 595,2800000000013). Mesuré, trouvé hors de ce
# poste — le geste de changer de jeu n'y est pour rien, l'ouverture seule le produit —
# et consigné au plan. On compare donc les dimensions au millionième de point.
for cle in ("PageWidth", "PageHeight"):
    if abs(origine[cle] - change[cle]) > 1e-6:
        sys.exit(f"ÉCHEC : « {cle} » a changé avec le jeu : {origine[cle]} → {change[cle]}.")
    del origine[cle], change[cle]
if origine != change:
    sys.exit("ÉCHEC : changer de jeu a réécrit autre chose que les jeux — une référence a-t-elle "
             "été remplacée par la couleur qu'elle désignait ?")
print("  1. changer de jeu ne réécrit que les deux jeux : les blocs gardent leurs références")

# --- les couleurs attendues, calculées depuis les valeurs des jeux ------------------
jeux = {
    "avant": {"main": "1A1A1A", "accent1": "2E5C8A", "accent2": "9DC3E6", "accent3": "E8A33D", "accent4": "6B7280"},
    "apres": {"main": "102A43", "accent1": "0B7285", "accent2": "99E9F2", "accent3": "F08C00", "accent4": "495057"},
}
def rvb(hexa):
    return tuple(int(hexa[i:i + 2], 16) for i in (0, 2, 4))

def teinte(c, pourcent):
    # La formule du noyau (TintFill) : vers le blanc sous 100.
    part = 1 - pourcent / 100
    return tuple(round(min(255, max(0, v + (255 - v) * part))) for v in c)

def sur(couleur, alpha, fond):
    a = alpha / 255
    return tuple(round(c * a + f * (1 - a)) for c, f in zip(couleur, fond))

def attendus(etat):
    j = {k: rvb(v) for k, v in jeux[etat].items()}
    fond = teinte(j["accent2"], 20)
    LIBRE_ACCENT1, LIBRE_MAIN, LIBRE_ACCENT2 = rvb("2E5C8A"), rvb("1A1A1A"), rvb("9DC3E6")
    # (quoi, x, y en points, couleur, tolérance, référence ?)
    return [
        ("le fond de page (teinte de l'accent 2)", 580, 800, fond, 2, True),
        ("l'aplat du jeu",                         115,  85, j["accent1"], 2, True),
        ("le contour du jeu",                     41.5,  85, j["main"], 2, True),
        ("l'aplat libre",                          295,  85, LIBRE_ACCENT1, 2, False),
        ("le contour libre",                     221.5,  85, LIBRE_MAIN, 2, False),
        ("le dégradé du jeu, à gauche",            401,  85, j["accent3"], 10, True),
        ("le dégradé du jeu, à droite",            549,  85, j["accent4"], 10, True),
        ("la teinte du jeu",                       115, 215, teinte(j["accent1"], 40), 2, True),
        ("la teinte libre",                        295, 215, teinte(LIBRE_ACCENT1, 40), 2, False),
        ("l'opacité propre (accent 4 à 50 %)",     475, 215, sur(j["accent4"], 0x80, fond), 3, True),
        ("le texte du jeu",                        113, 347, j["accent1"], 2, True),
        ("le fragment du jeu",                     272, 347, j["accent3"], 2, True),
        ("le fragment libre",                      431, 347, LIBRE_ACCENT1, 2, False),
        ("la case du jeu",                         115, 460, j["accent2"], 2, True),
        ("la case libre",                          265, 460, LIBRE_ACCENT2, 2, False),
    ]

def lire(chemin):
    donnees = open(chemin, "rb").read()
    champs, i = [], 2
    while len(champs) < 3:
        while donnees[i:i + 1].isspace(): i += 1
        if donnees[i:i + 1] == b"#":
            while donnees[i:i + 1] != b"\n": i += 1
            continue
        debut = i
        while not donnees[i:i + 1].isspace(): i += 1
        champs.append(int(donnees[debut:i]))
    largeur, hauteur, pixels = champs[0], champs[1], donnees[i + 1:]
    def couleur(x, y):
        k = (int(y * 2) * largeur + int(x * 2)) * 3
        return tuple(pixels[k:k + 3])
    def zone(x, y, l, h):
        # L'encre seule — les pixels franchement sombres — et non les pixels bruts : le
        # fond de page est une teinte du jeu, et il change sous chaque ligne.
        return [tuple(max(pixels[k:k + 3]) < 128 for k in range((yy * largeur + int(x * 2)) * 3,
                                                               (yy * largeur + int((x + l) * 2)) * 3, 3))
                for yy in range(int(y * 2), int((y + h) * 2))]
    return couleur, zone

rendus = {(r, e): lire(f"{travail}/{r}-{e}.ppm") for r in ("pdf", "ecran") for e in ("avant", "apres")}
NOMS = {"pdf": "PDF", "ecran": "écran"}

# --- 2 et 3. les couleurs, avant puis après -----------------------------------------
for etat in ("avant", "apres"):
    for rendu in ("pdf", "ecran"):
        couleur, _ = rendus[(rendu, etat)]
        for quoi, x, y, voulue, seuil, _ in attendus(etat):
            obtenue = couleur(x, y)
            if max(abs(a - b) for a, b in zip(obtenue, voulue)) > seuil:
                refuser(f"{NOMS[rendu]}, {etat} le changement — {quoi} : {obtenue} en ({x}, {y}) pt, "
                        f"{voulue} attendu.")
if echec:
    sys.exit(1)

points = attendus("apres")
print(f"  2. avant : les {len(points)} points portent les couleurs d'Ardoise, au PDF et à l'écran")
print(f"  3. après : {sum(p[5] for p in points)} points de référence aux couleurs d'Océan, "
      f"{sum(not p[5] for p in points)} points libres inchangés, au PDF et à l'écran")

# --- 4. les polices -------------------------------------------------------------------
polices = {e: open(f"{travail}/polices-{e}.txt").read() for e in ("avant", "apres")}
for nom, avant, apres in (("Liberation Serif", False, True), ("Liberation Sans", False, True),
                          ("DejaVu Serif", True, True)):
    for etat, voulu in (("avant", avant), ("apres", apres)):
        if (nom in polices[etat]) != voulu:
            refuser(f"PDF, {etat} le changement : « {nom} » {'absente' if voulu else 'présente'}.")

lignes = [("le titre du jeu", "titre", 40, 530, 520, 60, True), ("le corps du jeu", "corps", 40, 610, 520, 40, True),
          ("la police libre", "libre", 40, 670, 520, 40, False)]
# L'encre est comparée pixel à pixel. Le fond de page change sous chaque ligne — c'est
# une teinte du jeu — et quelques pixels de lissage, au bord des lettres, passent d'un
# côté à l'autre du seuil : relevé, 3 pixels sur la ligne en police libre, au PDF comme
# à l'écran, contre 6 532 et 6 275 pour le titre, 2 148 et 2 170 pour le corps, qui
# changent de police. Une ligne est donc « inchangée » sous 100 pixels et « changée »
# au-delà de 1 000 ; les relevés s'impriment à chaque passage.
releves = []
for rendu in ("pdf", "ecran"):
    for quoi, court, x, y, l, h, doit_changer in lignes:
        avant = rendus[(rendu, "avant")][1](x, y, l, h)
        apres = rendus[(rendu, "apres")][1](x, y, l, h)
        ecart = sum(a != b for ra, rb in zip(avant, apres) for a, b in zip(ra, rb))
        releves.append(f"{NOMS[rendu]} {court} {ecart}")
        if doit_changer and ecart < 1000:
            refuser(f"{NOMS[rendu]} — {quoi} : l'encre n'a pas changé avec le jeu de polices "
                    f"({ecart} pixels différents).")
        if not doit_changer and ecart >= 100:
            refuser(f"{NOMS[rendu]} — {quoi} : l'encre a changé avec le jeu de polices "
                    f"({ecart} pixels différents).")
if echec:
    sys.exit(1)
print("  4. polices : titre et corps du jeu passent à Liberation Serif et Liberation Sans, "
      "la police libre reste DejaVu Serif, au PDF et à l'écran")
print("     pixels d'encre différents : " + " · ".join(releves))
PY

# ------------------------------------------------------------------------- 5. filet

Scripts/comparer-rendus.sh "$travail/apres.mypub" 1 2.5

echo "C5b : changer de jeu recolore et change de police les blocs qui y font référence, et eux seuls, à l'écran comme au PDF."
