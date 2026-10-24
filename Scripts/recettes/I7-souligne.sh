#!/usr/bin/env bash
#
# Recette du poste I7 — le souligné.
#
# Ce qu'elle éprouve :
#
#   1. le fichier — « Samples/recette-souligne.mypub » porte « "Underline": true » au bloc
#      et au fragment, et ses dix-sept sortes en objet ; il se relit et se réécrit au
#      caractère près. Chaque document livré — Samples et Templates, réenregistrés par le
#      poste — se réécrit de même : un document sans souligné n'y gagne que des « false »
#      et des « null », et ne bouge plus ;
#   2. le dessin — la page de recette porte une ligne par sorte, et deux paragraphes
#      justifiés. À l'écran (le banc) et au PDF, à quatre pixels par point, chaque trait
#      est MESURÉ contre la table « post » de sa police, lue ici dans le fichier : le haut
#      du trait à 0,15 pt près, l'épaisseur à 12 % près — deux fois celle de la police
#      pour un trait épais —, le second trait d'un double à une épaisseur du premier, la
#      vague sur trois épaisseurs de haut, et chaque motif à sa part de la ligne et à son
#      dessin : la longueur de ses tirets et le nombre de points entre deux tirets, qui
#      séparent le tiret du tiret long et le tiret-point du tiret-point-point. « Mots
#      seuls » laisse sous chaque blanc un trou de la largeur du blanc ; un souligné
#      continu court sans trou d'un bord à l'autre d'une ligne justifiée, sous ses blancs
#      élargis. Un exposant souligné porte son trait sous SA ligne de base, montée de son
#      décalage, à la place que la police donne à son corps ; et le trait a la couleur de
#      son texte, mesurée sur l'image en couleur. Et l'écran et le PDF posent le trait à la
#      même ordonnée, à 0,25 pt près ;
#   3. l'image — « comparer-rendus.sh » passe sur la page, à sa tolérance par défaut ;
#   4. l'import — ATA 21, dont dix-sept styles de caractère portent chacun une des
#      dix-sept sortes de Publisher (voir plus bas), se lit par libmspub en seize sortes
#      distinctes, et chaque caractère importé porte la sienne : le double en double ;
#   5. la frappe, la copie et la commande — les tests du poste, joués ici : le souligné
#      suit la frappe comme le gras (Q1), une copie du bloc le garde, et « Souligné »
#      bascule en un pas d'annulation, même au milieu d'une saisie.
#
# Le point 2 ne lit rien dans le code qu'on éprouve. La place et l'épaisseur du trait
# sortent de la table « post » du fichier de police que fontconfig désigne, la ligne de
# base de sa table « hhea » : comparer l'écran au PDF prouverait leur accord, jamais leur
# justesse — c'est l'enseignement de R4. Le PDF est rendu par pdftocairo, et non par
# pdftoppm : le rastériseur de poppler « ajuste » les traits fins à la grille des pixels,
# et rendait au relevé le trait de DejaVu Sans 18 pt, de 0,79 pt, en 0,75 pt posé 0,06 pt
# trop haut ; cairo le rend à 0,78 pt, à 0,01 pt de sa place.
#
# Le point 4 se joue sur un vrai .pub, lu par la vraie libmspub. Aucun des deux documents
# ATR ne souligne, ni aucun des cinq documents du CD de Publisher 98 : la recette en
# fabrique un. Chaque style de caractère d'ATA 21 porte, outre sa langue (0x12), une
# seconde propriété de quatre octets, d'identifiant 0x3E, que libmspub ne lit pas ; la
# recette en change l'identifiant en celui du souligné (0x1E) et la valeur en l'une des
# dix-sept sortes. Les flux du conteneur ne changent pas de taille : rien d'autre ne
# bouge : le relevé est celui d'ATA 21, soulignés mis à part. Un style dont le texte n'est que blancs —
# une marque de paragraphe — ne dit rien d'une sorte : la recette prend, pour chaque
# sorte, le premier style dont libmspub rend ensuite des caractères visibles.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

document="Samples/recette-souligne.mypub"
binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"
banc="MyPublisher.Banc/bin/Debug/net10.0/MyPublisher.Banc"
source_pub="MyPublisher/Books/ATA 21 - Air conditioning.pub"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# Le banc n'est compilé ni par « dotnet test » ni par le contrôle : sans cette étape, la
# moitié « écran » de la recette mesurerait un banc périmé.
if [ -z "${MYPUBLISHER_DEJA_COMPILE:-}" ] && ! dotnet build MyPublisher.sln -c Debug --nologo -v q > "$travail/compilation.txt" 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    cat "$travail/compilation.txt" >&2
    exit 1
fi

for outil in "$binaire" "$banc"; do
    if [ ! -x "$outil" ]; then
        echo "ÉCHEC : binaire introuvable après compilation ($outil)." >&2
        exit 1
    fi
done

# Le lecteur du .pub, cherché comme la recette R5 le cherche.
lecteur="${MYPUBLISHER_PUB2RAW:-$(Scripts/outils-pub.sh --chemin 2>/dev/null || true)}"
if [ -z "$lecteur" ] || [ ! -x "$lecteur" ]; then
    echo "ÉCHEC : « pub2raw » est introuvable." >&2
    echo "    sudo apt install libmspub-tools    ou    Scripts/outils-pub.sh" >&2
    exit 1
fi
export MYPUBLISHER_PUB2RAW="$lecteur"
if [ -d "$racine/.outils/mspub/lib" ]; then
    export LD_LIBRARY_PATH="$racine/.outils/mspub/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
fi

for famille in "DejaVu Sans" "Liberation Sans"; do
    trouvee="$(fc-match "$famille" --format='%{family}' 2>/dev/null || true)"
    case "$trouvee" in
        *"$famille"*) ;;
        *)  echo "ÉCHEC : $famille est introuvable (fontconfig rend « $trouvee »)." >&2
            echo "Sans elle, la table « post » lue ici n'est pas celle de la police dessinée." >&2
            exit 1 ;;
    esac
done

# --------------------------------------------------------------------- 1. le fichier

"$binaire" "$document" --resave "$travail/copie.mypub" > /dev/null
if ! diff -u "$document" "$travail/copie.mypub" > "$travail/ecart.diff"; then
    echo "ÉCHEC : relu puis réécrit, le document souligné n'est plus le même." >&2
    head -30 "$travail/ecart.diff" >&2
    exit 1
fi

python3 - "$document" <<'PY'
import json, sys
doc = json.load(open(sys.argv[1], encoding="utf-8"))
blocs = [b for p in doc["Pages"] for b in p["Frames"] if b["$kind"] == "text"]
au_bloc = sum(1 for b in blocs if b["Underline"] is True)
au_fragment = sum(1 for b in blocs for f in b.get("Spans", []) if f["Underline"] is True)
objets = sum(1 for b in blocs for s in [b["Underline"]] + [f["Underline"] for f in b.get("Spans", [])]
             if isinstance(s, dict))
if au_bloc == 0 or au_fragment == 0 or objets < 16:
    print(f"ÉCHEC : le document de recette doit porter « true » au bloc ({au_bloc}) et au fragment "
          f"({au_fragment}), et les autres sortes en objet ({objets}).", file=sys.stderr)
    sys.exit(1)
PY

livres=0
for fichier in Samples/*.mypub MyPublisher/Templates/*.mypub; do
    "$binaire" "$fichier" --resave "$travail/relu.mypub" > /dev/null
    if ! cmp -s "$fichier" "$travail/relu.mypub"; then
        echo "ÉCHEC : $fichier ne se réécrit pas au caractère près — un document à réenregistrer ?" >&2
        diff -u "$fichier" "$travail/relu.mypub" | head -20 >&2
        exit 1
    fi
    # Un document sans bloc de texte n'a pas de souligné à écrire : celui des fonds.
    if grep -q '"\$kind": "text"' "$fichier" && ! grep -q '"Underline": ' "$fichier"; then
        echo "ÉCHEC : $fichier n'écrit pas le souligné : il n'a pas été réenregistré." >&2
        exit 1
    fi
    livres=$((livres + 1))
done
echo "  1. le fichier : « true » au bloc et au fragment, $livres documents livrés réécrits au caractère près."

# ----------------------------------------------------------------------- 2. le dessin

"$binaire" "$document" --export "$travail/sortie.pdf" > /dev/null 2> "$travail/avertissements.txt"
if [ -s "$travail/avertissements.txt" ]; then
    echo "ÉCHEC : l'export avertit, et la mesure ne porterait plus sur ce qu'on croit." >&2
    cat "$travail/avertissements.txt" >&2
    exit 1
fi

pp=4
"$banc" rendre "$document" --sortie "$travail/ecran.png" --page 1 --pixels-par-point "$pp" --sans-reperes > /dev/null
pdftocairo -r "$((72 * pp))" -f 1 -l 1 -png -singlefile "$travail/sortie.pdf" "$travail/pdf"
magick "$travail/ecran.png" -colorspace gray "$travail/ecran.pgm"
magick "$travail/pdf.png" -colorspace gray "$travail/pdf.pgm"
# En couleur aussi : les niveaux de gris ne disent pas la teinte d'un trait.
magick "$travail/ecran.png" -alpha remove -alpha off -depth 8 "$travail/ecran.ppm"
magick "$travail/pdf.png" -alpha remove -alpha off -depth 8 "$travail/pdf.ppm"
pdftotext -bbox "$travail/sortie.pdf" "$travail/boites.html"

python3 - "$document" "$travail/boites.html" "$travail/ecran.pgm" "$travail/pdf.pgm" "$pp" \
    "$travail/ecran.ppm" "$travail/pdf.ppm" <<'PY'
import json, re, struct, subprocess, sys

document, boites, ecran, pdf, pp = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4], int(sys.argv[5])
ecran_couleur, pdf_couleur = sys.argv[6], sys.argv[7]

def lire_pgm(chemin):
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
    return champs[0], champs[1], donnees[i + 1:]

def police(famille):
    """Ce que la police dit d'elle-même, lu dans son fichier : l'unité de l'em, la montée
    et la hauteur naturelle (« hhea »), le haut et l'épaisseur du souligné (« post »)."""
    fichier = subprocess.run(["fc-match", famille, "--format=%{file}"],
                             capture_output=True, text=True, check=True).stdout
    d = open(fichier, "rb").read()
    tables = {}
    for i in range(struct.unpack(">H", d[4:6])[0]):
        etiquette, _, position, _ = struct.unpack(">4sIII", d[12 + 16 * i:28 + 16 * i])
        tables[etiquette] = position
    em = struct.unpack(">H", d[tables[b"head"] + 18:tables[b"head"] + 20])[0]
    montee, descente, interligne = struct.unpack(">hhh", d[tables[b"hhea"] + 4:tables[b"hhea"] + 10])
    position, epaisseur = struct.unpack(">hh", d[tables[b"post"] + 8:tables[b"post"] + 12])
    return dict(em=em, montee=montee, naturelle=montee - descente + interligne,
                position=position, epaisseur=epaisseur)

doc = json.load(open(document, encoding="utf-8"))
blocs = doc["Pages"][0]["Frames"]
mots = [(float(a), float(b), float(c), t) for a, b, c, t in re.findall(
    r'<word xMin="([0-9.]+)" yMin="([0-9.]+)" xMax="([0-9.]+)" yMax="[0-9.]+">([^<]*)</word>',
    open(boites, encoding="utf-8").read())]
images = {"écran": lire_pgm(ecran), "PDF": lire_pgm(pdf)}
couleurs = {"écran": lire_pgm(ecran_couleur), "PDF": lire_pgm(pdf_couleur)}

def du_bloc(bloc):
    """Les mots d'un bloc, dans son cadre : deux blocs côte à côte partagent des ordonnées."""
    return [m for m in mots if bloc["Y"] <= m[1] < bloc["Y"] + bloc["Height"]
            and bloc["X"] - 1 <= m[0] < bloc["X"] + bloc["Width"]]

def encre(image, x, y):
    largeur, hauteur, pixels = image
    if x < 0 or y < 0 or x >= largeur or y >= hauteur: return 0.0
    return (255 - pixels[y * largeur + x]) / 255

def teinte(couleur):
    """« #AARRGGBB » en rouge, vert, bleu."""
    return tuple(int(couleur[i:i + 2], 16) for i in (3, 5, 7))

def couverture(image, x, y, c):
    """La part d'un pixel de l'image en couleur que couvre une encre de teinte c : sa place
    sur le chemin qui va du blanc à c. Les niveaux de gris sous-estimeraient l'encre d'un
    texte de couleur, et l'épaisseur de son trait avec elle. Une encre plus sombre que c —
    du noir à la place du rouge — couvre plus que le pixel entier."""
    largeur, hauteur, pixels = image
    if x < 0 or y < 0 or x >= largeur or y >= hauteur: return 0.0
    i = 3 * (y * largeur + x)
    return (sum((255 - pixels[i + k]) * (255 - c[k]) for k in range(3))
            / sum((255 - v) ** 2 for v in c))

def passages(image, x, haut, bas, mesure=encre):
    """Les passages d'encre de la colonne x entre deux ordonnées en points : pour chacun,
    son centre et son épaisseur, en points. L'épaisseur est la somme des couvertures de
    ses pixels : le lissage d'un bord n'y change rien."""
    trouves, courant = [], None
    for y in range(int(haut * pp), int(bas * pp) + 1):
        c = mesure(image, x, y)
        if c > 0.03:
            courant = courant or [0.0, 0.0]
            courant[0] += c
            courant[1] += c * (y + 0.5)
        elif courant:
            trouves.append((courant[1] / courant[0] / pp, courant[0] / pp))
            courant = None
    if courant: trouves.append((courant[1] / courant[0] / pp, courant[0] / pp))
    return trouves

def profil(image, x0, x1, y0, y1):
    """Le trait est-il là, colonne par colonne de x0 à x1, dans les rangées de y0 à y1 ?
    Le seuil est la moitié de l'encre la plus noire de la bande : un texte de couleur
    n'encre pas un pixel autant qu'un texte noir."""
    colonnes = range(int(x0 * pp), int(x1 * pp) + 1)
    rangees = range(int(y0 * pp), int(y1 * pp) + 1)
    plus = [max(encre(image, x, y) for y in rangees) for x in colonnes]
    seuil = 0.5 * max(plus)
    return [c > seuil for c in plus]

def trous(presence):
    """Les suites de colonnes sans trait entre la première et la dernière qui en portent :
    où le trait commence et finit, et la largeur de chaque trou, en points."""
    if True not in presence: return None
    premier = presence.index(True)
    dernier = len(presence) - 1 - presence[::-1].index(True)
    largeurs, courant = [], 0
    for p in presence[premier:dernier + 1]:
        if p:
            if courant: largeurs.append(courant / pp)
            courant = 0
        else:
            courant += 1
    return premier / pp, dernier / pp, largeurs

def dessin(presence, t):
    """Le dessin d'un motif le long de la ligne : la longueur de ses traits, en épaisseurs,
    sans le premier ni le dernier, qu'un bout de ligne peut écourter."""
    longueurs, courant = [], 0
    for p in presence:
        if p:
            courant += 1
        elif courant:
            longueurs.append(courant / pp / t)
            courant = 0
    if courant: longueurs.append(courant / pp / t)
    return longueurs[1:-1]

def mediane(valeurs):
    valeurs = sorted(valeurs)
    return valeurs[len(valeurs) // 2] if valeurs else float("nan")

echec = False
def refuser(message):
    global echec
    echec = True
    print("ÉCHEC : " + message, file=sys.stderr)

def souligne(bloc):
    s = bloc.get("Underline")
    for f in bloc.get("Spans") or []:
        if f.get("Underline") is not None: s = f["Underline"]
    if s is True: return dict(Line="Solid", Double=False, Thick=False, WordsOnly=False)
    return s or None

# La part de la ligne que couvre chaque motif, sur une période : le point aussi long que le
# blanc qui le suit, le tiret de quatre épaisseurs pour deux de blanc, le tiret long de
# huit pour trois. Un motif ramené à un trait plein couvrirait toute la ligne.
couvert = {"Solid": 1.0, "Dotted": 1 / 2, "Dash": 4 / 6, "LongDash": 8 / 11,
           "DotDash": 5 / 9, "DotDotDash": 6 / 12}
# La part ne sépare pas les voisins : 67 % pour le tiret, 73 % pour le tiret long, 56 et
# 50 % pour le tiret-point et le tiret-point-point, à 0,1 près. Leur dessin les sépare :
# la longueur du tiret, en épaisseurs, et le nombre de points qui suivent chaque tiret.
# Le pointillé n'a que des points. Un trait d'au plus deux épaisseurs et demie est un point.
motifs = {"Dotted": (None, None), "Dash": (4, 0), "LongDash": (8, 0), "DotDash": (4, 1), "DotDotDash": (4, 2)}

def decale(bloc):
    return any(f.get("BaselineShift") for f in bloc.get("Spans") or [])

sortes = 0
for bloc in blocs:
    s = souligne(bloc)
    if not s or bloc["Align"] == "Justify" or decale(bloc): continue
    sortes += 1
    p = police(bloc["FontFamily"])
    corps = bloc["FontSize"]
    base = bloc["Y"] + p["montee"] * corps / p["em"]
    haut = base - p["position"] * corps / p["em"]
    t = p["epaisseur"] * corps / p["em"] * (2 if s["Thick"] else 1)
    ligne = sorted(du_bloc(bloc), key=lambda m: m[0])
    blancs = [b[0] - a[2] for a, b in zip(ligne, ligne[1:])]
    gauche, droite = ligne[0][0] - 3, ligne[-1][2] + 3
    nom = f"« {bloc['Name']} » ({bloc['FontFamily']})"
    mesures = {}

    for cote, image in images.items():
        # Les colonnes sans lettre — un blanc, ou le jour entre deux lettres : rien
        # au-dessus de la ligne de base. Ce qu'on y trouve dessous est le trait seul.
        libres = [x for x in range(int(gauche * pp), int(droite * pp))
                  if not any(encre(image, x, y) > 0.03
                             for y in range(int((base - 0.7 * corps) * pp), int((base - 0.3) * pp)))]
        colonnes = [c for c in (passages(image, x, base - 0.3, base + 0.6 * corps) for x in libres) if c]
        if not colonnes:
            refuser(f"{nom}, {cote} : aucun trait sous la ligne.")
            continue

        nombre = 2 if s["Double"] else 1
        pleines = [c for c in colonnes if len(c) == nombre]
        if len(pleines) < 0.5 * len(colonnes):
            refuser(f"{nom}, {cote} : {len(pleines)} colonne(s) sur {len(colonnes)} portent {nombre} trait(s).")
            continue
        premiers = [c[0] for c in pleines]
        epaisseur = mediane([e for _, e in premiers])
        if s["Line"] == "Wave":
            # La vague passe à une autre hauteur dans chaque colonne : son haut est celui
            # de ses crêtes, et elle couvre de ses crêtes à ses creux.
            dessus = min(y - e / 2 for y, e in premiers)
            dessous = max(y + e / 2 for y, e in premiers)
        else:
            dessus = mediane([y - e / 2 for y, e in premiers])
            dessous = mediane([y + e / 2 for y, e in premiers])
        m = dict(dessus=dessus, epaisseur=epaisseur, etendue=dessous - dessus)
        if nombre == 2:
            m["ecart"] = mediane([c[1][0] - c[0][0] for c in pleines])

        # Le dessin, le long de la ligne : relevé sur la rangée de l'axe du trait,
        # qu'aucune lettre de ce texte sans jambage n'atteint.
        if s["Line"] != "Wave":
            axe = haut + t / 2
            presence = profil(image, gauche, droite, axe - t / 4, axe + t / 4)
            bornes = trous(presence)
            m["trous"] = bornes[2] if bornes else []
            m["part"] = sum(presence) / max(1, len(presence) - int(6 * pp))
            m["dessin"] = dessin(presence, t)
        mesures[cote] = m

        # Le haut du trait est où la police le met, et chaque trait a son épaisseur.
        if abs(dessus - haut) > 0.15:
            refuser(f"{nom}, {cote} : le haut du trait est à {dessus - base:.2f} pt sous la ligne de "
                    f"base, la table « post » le met à {haut - base:.2f}.")
        if s["Line"] == "Wave":
            # Une vague monte et descend d'une épaisseur autour de son axe : son encre
            # couvre trois épaisseurs de haut.
            if abs(m["etendue"] - 3 * t) > 0.15 * t + 0.1:
                refuser(f"{nom}, {cote} : la vague couvre {m['etendue']:.2f} pt de haut, {3 * t:.2f} attendus.")
        elif abs(epaisseur - t) > 0.12 * t + 0.05:
            refuser(f"{nom}, {cote} : le trait a {epaisseur:.2f} pt d'épaisseur, la police en donne {t:.2f}.")
        if nombre == 2:
            # Une épaisseur de blanc entre les deux traits ; entre le creux de la première
            # vague et la crête de la seconde pour une vague double.
            attendu = (4 if s["Line"] == "Wave" else 2) * t
            if abs(m["ecart"] - attendu) > 0.15 * t + 0.05:
                refuser(f"{nom}, {cote} : les deux traits sont à {m['ecart']:.2f} pt l'un de l'autre, "
                        f"{attendu:.2f} attendus.")

        # Le dessin : continu, à son motif, ou rien sous les blancs pour « mots seuls ».
        if s["Line"] != "Wave":
            if s["WordsOnly"]:
                if len(m["trous"]) != len(blancs):
                    refuser(f"{nom}, {cote} : {len(m['trous'])} interruption(s) du trait, une par "
                            f"blanc attendue ({len(blancs)}).")
                else:
                    for trou, blanc in zip(m["trous"], blancs):
                        if abs(trou - blanc) > 0.5:
                            refuser(f"{nom}, {cote} : un trou de {trou:.2f} pt sous un blanc de {blanc:.2f}.")
            elif s["Line"] == "Solid":
                if m["trous"]:
                    refuser(f"{nom}, {cote} : le trait plein s'interrompt {len(m['trous'])} fois.")
            else:
                if abs(m["part"] - couvert[s["Line"]]) > 0.1:
                    refuser(f"{nom}, {cote} : le motif couvre {m['part']:.0%} de la ligne, "
                            f"{couvert[s['Line']]:.0%} attendus.")
                tiret, apres = motifs[s["Line"]]
                tirets = [l for l in m["dessin"] if l > 2.5]
                points = [l for l in m["dessin"] if l <= 2.5]
                lu = [round(l, 1) for l in m["dessin"][:8]]
                if tiret is None:
                    if tirets or not points or abs(mediane(points) - 1) > 0.5:
                        refuser(f"{nom}, {cote} : un pointillé n'a que des points d'une épaisseur ; "
                                f"traits relevés, en épaisseurs : {lu}.")
                elif len(tirets) < 3:
                    refuser(f"{nom}, {cote} : {len(tirets)} tiret(s) relevé(s), trois au moins attendus ; {lu}.")
                else:
                    if abs(mediane(tirets) - tiret) > 1.2:
                        refuser(f"{nom}, {cote} : des tirets de {mediane(tirets):.1f} épaisseurs, {tiret} attendues ; {lu}.")
                    if round(len(points) / len(tirets)) != apres:
                        refuser(f"{nom}, {cote} : {len(points)} point(s) pour {len(tirets)} tirets, "
                                f"{apres} par tiret attendu(s) ; {lu}.")
                m["motif"] = f"tirets {mediane(tirets):.1f}, points {len(points)}/{len(tirets)}" if tirets else \
                    f"points {mediane(points) if points else 0:.1f}"

    if len(mesures) == 2:
        e, d = mesures["écran"], mesures["PDF"]
        if abs(e["dessus"] - d["dessus"]) > 0.25:
            refuser(f"{nom} : le trait est à {e['dessus'] - base:.2f} pt sous la ligne de base à "
                    f"l'écran, à {d['dessus'] - base:.2f} au PDF.")
    print(f"    {bloc['Name']:24} {bloc['FontFamily']:16} post : haut {haut - base:4.2f} ép. {t:4.2f} — "
          + " — ".join(f"{c} : haut {m['dessus'] - base:4.2f} ép. {m['epaisseur']:4.2f}"
                       + (f" part {m['part']:4.0%}" if "part" in m else f" vague {m['etendue']:4.2f}")
                       + (f" ({m['motif']})" if "motif" in m else "")
                       for c, m in mesures.items()))

# Les lignes justifiées : un souligné continu court d'un bord à l'autre de la colonne,
# sous les blancs élargis ; « mots seuls » laisse chaque blanc nu, si large soit-il.
justifiees = 0
for bloc in blocs:
    s = souligne(bloc)
    if not s or bloc["Align"] != "Justify": continue
    p = police(bloc["FontFamily"])
    corps = bloc["FontSize"]
    naturelle = p["naturelle"] * corps / p["em"]
    t = p["epaisseur"] * corps / p["em"]
    lignes = {}
    for m in du_bloc(bloc):
        lignes.setdefault(round(m[1], 1), []).append(m)
    rangs = sorted(lignes)
    # La dernière ligne d'un paragraphe ne se justifie pas : on ne mesure que les autres.
    for k, cle in enumerate(rangs[:-1]):
        justifiees += 1
        ligne = sorted(lignes[cle], key=lambda m: m[0])
        base = bloc["Y"] + k * naturelle + p["montee"] * corps / p["em"]
        axe = base - p["position"] * corps / p["em"] + t / 2
        blancs = [b[0] - a[2] for a, b in zip(ligne, ligne[1:])]
        for cote, image in images.items():
            bornes = trous(profil(image, bloc["X"] - 2, bloc["X"] + bloc["Width"] + 2, axe - t / 4, axe + t / 4))
            nom = f"« {bloc['Name']} », ligne {k + 1}, {cote}"
            if bornes is None:
                refuser(f"{nom} : aucun trait.")
                continue
            debut, fin, largeurs = bornes[0] + bloc["X"] - 2, bornes[1] + bloc["X"] - 2, bornes[2]
            if abs(debut - bloc["X"]) > 0.5 or abs(fin - (bloc["X"] + bloc["Width"])) > 0.5:
                refuser(f"{nom} : le trait court de {debut:.2f} à {fin:.2f} pt, la colonne de "
                        f"{bloc['X']} à {bloc['X'] + bloc['Width']}.")
            if s["WordsOnly"]:
                if len(largeurs) != len(blancs) or any(abs(a - b) > 0.5 for a, b in zip(largeurs, blancs)):
                    refuser(f"{nom} : trous {[round(x, 2) for x in largeurs]} sous des blancs de "
                            f"{[round(x, 2) for x in blancs]}.")
            elif largeurs:
                refuser(f"{nom} : le trait continu s'interrompt sous {len(largeurs)} blanc(s) élargi(s).")
        print(f"    {bloc['Name']:24} ligne {k + 1} : {len(ligne)} mots, blancs élargis à "
              f"{mediane(blancs):.2f} pt — trait mesuré à l'écran et au PDF")

# L'exposant souligné, et la couleur du trait. Le trait d'un morceau se pose sous SA ligne
# de base, montée de son décalage comme son texte, à la place et à l'épaisseur que la
# police donne à SON corps : sous « 12 », ni sous la ligne de base du texte courant, ni à
# l'épaisseur du grand corps. Mesurés sur l'image en couleur, par la couverture d'une
# encre de la teinte du texte : le trait de chaque morceau a la couleur de son texte, et
# un trait noir sous un texte rouge se voit, ce que les niveaux de gris ne diraient pas.
exposants = 0
for bloc in blocs:
    if not decale(bloc): continue
    p = police(bloc["FontFamily"])
    corps = bloc["FontSize"]
    base = bloc["Y"] + p["montee"] * corps / p["em"]
    c = teinte(bloc["Foreground"])
    tous = sorted(du_bloc(bloc), key=lambda m: m[0])
    parts = []
    for f in bloc["Spans"]:
        if not f.get("BaselineShift"): continue
        corps_f = f["FontSize"] or corps
        texte = bloc["Text"][f["Start"]:f["Start"] + f["Length"]]
        parts.append((f"l'exposant « {texte} »", corps_f, base - f["BaselineShift"] * corps_f,
                      [m for m in tous if m[3] == texte]))
    parts.append(("le texte courant", corps, base, [m for m in tous if all(m not in q[3] for q in parts)]))

    for quoi, corps_m, base_m, ses_mots in parts:
        nom = f"« {bloc['Name']} », {quoi}"
        if not ses_mots:
            refuser(f"{nom} : introuvable dans le PDF.")
            continue
        haut = base_m - p["position"] * corps_m / p["em"]
        t = p["epaisseur"] * corps_m / p["em"]
        dessus_par_cote = {}
        for cote, image in couleurs.items():
            def mesure(image, x, y): return couverture(image, x, y, c)
            # Une marge d'un point et demi de chaque côté de chaque mot : l'écran ne pose
            # pas ses mots exactement où le PDF les pose.
            xs = [x for m in ses_mots for x in range(int((m[0] + 1.5) * pp), int((m[2] - 1.5) * pp))]
            libres = [x for x in xs if not any(mesure(image, x, y) > 0.05 for y in
                                              range(int((base_m - 0.7 * corps_m) * pp), int((base_m - 0.3) * pp)))]
            # Jusque sous la ligne de base du texte courant : un trait qui n'aurait pas
            # monté avec son exposant y serait.
            colonnes = [q for q in (passages(image, x, base_m - 0.3, base + 0.6 * corps, mesure) for x in libres) if q]
            pleines = [q[0] for q in colonnes if len(q) == 1]
            if len(pleines) < 3 or len(pleines) < 0.5 * len(colonnes):
                refuser(f"{nom}, {cote} : {len(pleines)} colonne(s) portent un trait seul, "
                        f"sur {len(colonnes)} sous {len(libres)} colonne(s) sans lettre.")
                continue
            dessus = mediane([y - e / 2 for y, e in pleines])
            epaisseur = mediane([e for _, e in pleines])
            dessus_par_cote[cote] = dessus
            if abs(dessus - haut) > 0.15:
                refuser(f"{nom}, {cote} : le haut du trait est à {dessus - base:.2f} pt sous la ligne de base du "
                        f"texte ; sous sa ligne de base décalée, la table « post » le met à {haut - base:.2f}.")
            if abs(epaisseur - t) > 0.12 * t + 0.05:
                refuser(f"{nom}, {cote} : le trait a {epaisseur:.2f} pt d'épaisseur, la police en donne "
                        f"{t:.2f} au corps {corps_m}.")
            # La teinte : les pixels que le trait couvre en entier, au cœur du trait.
            pleins = [couleurs[cote][2][3 * (y * couleurs[cote][0] + x) + k]
                      for x in libres for y in range(int((dessus + 0.2 * t) * pp), int((dessus + 0.8 * t) * pp) + 1)
                      if mesure(image, x, y) > 0.85 for k in range(3)]
            vue = tuple(mediane(pleins[k::3]) for k in range(3))
            if not pleins or max(abs(a - b) for a, b in zip(vue, c)) > 40:
                refuser(f"{nom}, {cote} : le trait est de teinte {vue}, son texte de {c}.")
        if len(dessus_par_cote) == 2 and abs(dessus_par_cote["écran"] - dessus_par_cote["PDF"]) > 0.25:
            refuser(f"{nom} : le trait est à {dessus_par_cote['écran'] - base:.2f} pt sous la ligne de base à "
                    f"l'écran, à {dessus_par_cote['PDF'] - base:.2f} au PDF.")
        exposants += 1
        print(f"    {bloc['Name']:24} {quoi:22} post : haut {haut - base:5.2f} ép. {t:4.2f} — "
              + " — ".join(f"{k} : haut {v - base:5.2f}" for k, v in dessus_par_cote.items())
              + f", teinte {bloc['Foreground']}")

if sortes != 17 or justifiees < 4 or exposants < 2:
    refuser(f"{sortes} sortes, {justifiees} lignes justifiées et {exposants} morceaux décalés ou non mesurés ; "
            "dix-sept, quatre et deux au moins attendus.")
sys.exit(1 if echec else 0)
PY
echo "  2. le dessin : chaque sorte à la place et à l'épaisseur de la table « post », à son motif, l'exposant sous sa ligne de base, à la teinte de son texte, à l'écran et au PDF."

# ------------------------------------------------------------------------- 3. l'image

# La tolérance par défaut, et non une mesure faite pour ce document : mesuré, il s'écarte
# de 2,25 %, dont 1,65 % pour son texte seul — le lissage des glyphes, comme partout.
Scripts/comparer-rendus.sh "$document" 1 > "$travail/comparaison.txt" 2>&1 || {
    sed 's/^/    /' "$travail/comparaison.txt" >&2
    echo "ÉCHEC : l'écran et le PDF du document souligné ne donnent pas la même image." >&2
    exit 1
}
echo "  3. l'image : $(head -1 "$travail/comparaison.txt")"

# ------------------------------------------------------------------------ 4. l'import

python3 - "$source_pub" "$travail/souligne.pub" "$lecteur" "$travail/releve.txt" <<'PY'
import re, struct, subprocess, sys

source, cible, lecteur, sortie = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]

def conteneur(donnees):
    """Le fichier composé (OLE) : la taille d'un secteur, la table d'allocation et les
    entrées du répertoire. Rien de plus que ce qu'il faut pour trouver un flux."""
    assert donnees[:8] == bytes.fromhex("D0CF11E0A1B11AE1"), "ce n'est pas un fichier composé"
    taille = 1 << struct.unpack_from("<H", donnees, 30)[0]
    nfat, premier_rep = struct.unpack_from("<I", donnees, 44)[0], struct.unpack_from("<I", donnees, 48)[0]
    difat = list(struct.unpack_from("<109I", donnees, 76))
    suite, nombre = struct.unpack_from("<II", donnees, 68)
    for _ in range(nombre):
        bloc = struct.unpack_from(f"<{taille // 4}I", donnees, 512 + suite * taille)
        difat += bloc[:-1]
        suite = bloc[-1]
    fat = []
    for secteur in difat[:nfat]:
        fat += struct.unpack_from(f"<{taille // 4}I", donnees, 512 + secteur * taille)
    def chaine(premier):
        s, liste = premier, []
        while s < 0xFFFFFFFA:
            liste.append(s)
            s = fat[s]
        return liste
    repertoire = b"".join(donnees[512 + s * taille:512 + (s + 1) * taille] for s in chaine(premier_rep))
    entrees = []
    for i in range(len(repertoire) // 128):
        e = repertoire[i * 128:(i + 1) * 128]
        n = struct.unpack_from("<H", e, 64)[0]
        gauche, droite, enfant = struct.unpack_from("<III", e, 68)
        debut, longueur = struct.unpack_from("<II", e, 116)
        entrees.append(dict(nom=e[:max(0, n - 2)].decode("utf-16-le"), gauche=gauche, droite=droite,
                            enfant=enfant, debut=debut, longueur=longueur))
    return taille, chaine, entrees

def enfant(entrees, parent, nom):
    """L'entrée « nom » parmi les enfants de « parent » : un arbre de frères à parcourir."""
    pile = [entrees[parent]["enfant"]]
    while pile:
        i = pile.pop()
        if i >= len(entrees): continue
        if entrees[i]["nom"] == nom: return i
        pile += [entrees[i]["gauche"], entrees[i]["droite"]]
    raise SystemExit(f"ÉCHEC : « {nom} » introuvable dans le .pub.")

donnees = open(source, "rb").read()
taille, chaine, entrees = conteneur(donnees)
flux_i = enfant(entrees, enfant(entrees, enfant(entrees, 0, "Quill"), "QuillSub"), "CONTENTS")
secteurs = chaine(entrees[flux_i]["debut"])
flux = b"".join(donnees[512 + s * taille:512 + (s + 1) * taille] for s in secteurs)[:entrees[flux_i]["longueur"]]
def dans_le_fichier(position): return 512 + secteurs[position // taille] * taille + position % taille

# Les morceaux du texte de Quill, puis les styles de caractère (FDPC), comme libmspub les
# lit (MSPUBParser::parseQuill, parseCharacterStyles, getCharacterStyle).
morceaux, liste = [], 0x18
while liste != 0xFFFFFFFF:
    nombre, suivante = struct.unpack_from("<H", flux, liste + 2)[0], struct.unpack_from("<I", flux, liste + 4)[0]
    for i in range(nombre):
        p = liste + 8 + 24 * i
        morceaux.append((flux[p + 2:p + 6].decode("latin-1"),) + struct.unpack_from("<II", flux, p + 16))
    liste = suivante

LONGUEUR = {0x5: 0, 0x8: 0, 0xa: 0, 0x78: 0, 0x10: 2, 0x12: 2, 0x18: 2, 0x1a: 2, 0x07: 2,
            0x20: 4, 0x22: 4, 0x58: 4, 0x68: 4, 0x70: 4, 0xb8: 4, 0x28: 8, 0x38: 16, 0x48: 24}
candidats = []
for nom, debut, _ in morceaux:
    if nom != "FDPC": continue
    n = struct.unpack_from("<H", flux, debut)[0]
    for decalage in struct.unpack_from(f"<{n}H", flux, debut + 8 + 4 * n):
        style = debut + decalage
        fin, q = style + struct.unpack_from("<I", flux, style)[0], style + 4
        while q < fin:
            ident, genre = flux[q], flux[q + 1]
            if ident == 0x3E and genre == 0x22:
                if q not in candidats: candidats.append(q)
                break
            longueur = LONGUEUR.get(genre, -1)
            q += 2 + (longueur if longueur >= 0 else struct.unpack_from("<I", flux, q + 2)[0])

# Les dix-sept valeurs de Publisher (readUnderline, MSPUBParser.cpp:128-172).
SORTES = [0x1, 0x2, 0x3, 0x4, 0x6, 0x7, 0x9, 0xa, 0xb, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17]

def relever(octets):
    open(cible, "wb").write(octets)
    # En octets, puis décodé : le mode texte de Python changerait en saut de ligne le
    # « \r » de la marque de paragraphe, que libmspub écrit dans « insertText (\r) ».
    return subprocess.run([lecteur, cible], capture_output=True, check=True).stdout.decode("utf-8")

def visibles(releve):
    """Les sortes rendues par le relevé, sur des caractères visibles : chaque quadruplet
    de réglages, et le nombre de fragments qui le portent."""
    compte, courant = {}, None
    for ligne in releve.split("\n"):
        nu = ligne.strip()
        if nu.startswith("openSpan"):
            reglages = dict(re.findall(r"style:text-underline-([a-z]+): ([a-z-]+)", nu))
            courant = tuple(reglages.get(k) for k in ("style", "type", "width", "mode")) if reglages else None
        elif nu.startswith("insertText") and courant and nu[nu.index("(") + 1:nu.rindex(")")].replace("\r", "").strip():
            compte[courant] = compte.get(courant, 0) + 1
    return compte

patche = bytearray(donnees)
origine = relever(bytes(patche))
compte = visibles(origine)
if compte:
    raise SystemExit(f"ÉCHEC : ATA 21 souligne déjà ({compte}) : la recette ne prouverait rien.")

for valeur in SORTES:
    for position in list(candidats):
        essai = bytearray(patche)
        essai[dans_le_fichier(position)] = 0x1E
        for k in range(4): essai[dans_le_fichier(position + 2 + k)] = (valeur >> (8 * k)) & 0xFF
        apres = visibles(relever(bytes(essai)))
        if sum(apres.values()) > sum(compte.values()):
            patche, compte = essai, apres
            candidats.remove(position)
            break
    else:
        raise SystemExit(f"ÉCHEC : aucun style d'ATA 21 ne porte la sorte {valeur:#x} sur du texte visible.")

releve = relever(bytes(patche))
open(sortie, "w", encoding="utf-8", newline="").write(releve)
# Le document n'a pas changé d'autre chose que de ses soulignés : autant d'appels de dessin,
# les mêmes, à leurs réglages de souligné près.
def sans_souligne(texte): return re.sub(r", style:text-underline-[a-z]+: [a-z-]+", "", texte)
if sans_souligne(releve) != sans_souligne(origine):
    raise SystemExit("ÉCHEC : le .pub patché ne se relit plus comme ATA 21, soulignés mis à part.")
if len(compte) != 16:
    raise SystemExit(f"ÉCHEC : libmspub rend {len(compte)} sortes distinctes des dix-sept, seize attendues.")
if not any(sorte[1] == "double" for sorte in compte):
    raise SystemExit("ÉCHEC : aucun fragment n'est rendu « style:text-underline-type: double ».")
print(f"    dix-sept styles d'ATA 21 soulignés : libmspub en rend {len(compte)} sortes, "
      f"sur {sum(compte.values())} fragments visibles")
PY

"$binaire" "$travail/souligne.pub" --resave "$travail/importe.mypub" > /dev/null

python3 - "$travail/releve.txt" "$travail/importe.mypub" <<'PY'
import json, sys

releve, importe = sys.argv[1], sys.argv[2]

def sorte_odf(reglages):
    """Ce qu'un fragment du relevé doit devenir : la sorte d'OpenDocument, lue ici à part
    du code éprouvé — le dessin, double ou non, épais ou non, mots seuls ou non."""
    if not reglages or reglages.get("type") == "none" or reglages.get("style") == "none":
        return None
    dessins = {"solid": "Solid", "dotted": "Dotted", "dash": "Dash", "long-dash": "LongDash",
               "dot-dash": "DotDash", "dot-dot-dash": "DotDotDash", "wave": "Wave"}
    return (dessins[reglages.get("style", "solid")], reglages.get("type") == "double",
            reglages.get("width") == "bold", reglages.get("mode") == "skip-white-space")

def sorte_json(valeur):
    if valeur is None or valeur is False: return None
    if valeur is True: return ("Solid", False, False, False)
    return (valeur["Line"], valeur["Double"], valeur["Thick"], valeur["WordsOnly"]) if valeur["Line"] != "None" else None

# Le texte de chaque bloc, caractère par caractère avec sa sorte, tel que le relevé le
# dit et que l'import l'assemble : un saut de ligne entre deux paragraphes, la marque de
# paragraphe de Publisher (« \r ») écartée.
import re
attendus, texte, sortes, courant, tableau = [], None, None, None, 0
for ligne in open(releve, encoding="utf-8", newline="").read().split("\n"):
    nu = ligne.strip()
    appel = nu.split("(")[0].strip()
    brut = nu[nu.index("(") + 1:nu.rindex(")")] if "(" in nu and ")" in nu else ""
    if appel == "startTableObject": tableau += 1
    elif appel == "endTableObject": tableau -= 1
    elif appel == "startTextObject": texte, sortes = [], []
    elif appel == "openParagraph" and texte is not None and texte:
        texte.append("\n"); sortes.append(None)
    elif appel == "openSpan":
        courant = sorte_odf(dict(re.findall(r"style:text-underline-([a-z]+): ([a-z-]+)", brut)))
    elif appel == "closeSpan": courant = None
    elif appel == "insertText" and texte is not None:
        for c in brut.replace("\r", ""): texte.append(c); sortes.append(courant)
    elif appel == "insertTab" and texte is not None: texte.append("\t"); sortes.append(courant)
    elif appel == "insertLineBreak" and texte is not None: texte.append("\n"); sortes.append(courant)
    elif appel == "endTextObject" and texte is not None:
        if tableau == 0: attendus.append(("".join(texte), sortes))
        texte = None

doc = json.load(open(importe, encoding="utf-8"))
blocs = [b for page in doc["Pages"] for b in page["Frames"] if b["$kind"] == "text"]
if len(blocs) != len(attendus):
    raise SystemExit(f"ÉCHEC : {len(blocs)} blocs de texte importés pour {len(attendus)} dans le relevé.")

echec, soulignes, vus = False, 0, set()
for bloc, (texte, sortes) in zip(blocs, attendus):
    if bloc["Text"] != texte:
        raise SystemExit("ÉCHEC : le texte importé n'est pas celui du relevé : la comparaison serait décalée.")
    # La sorte de chaque caractère importé : celle du bloc, puis celle des fragments qui
    # le couvrent, le dernier l'emportant ; un fragment nul laisse celle du bloc.
    importees = [sorte_json(bloc["Underline"])] * len(texte)
    for f in bloc.get("Spans", []):
        if f["Underline"] is None: continue
        for i in range(max(0, f["Start"]), min(len(texte), f["Start"] + f["Length"])):
            importees[i] = sorte_json(f["Underline"])
    for i, c in enumerate(texte):
        if c.isspace(): continue
        if importees[i] != sortes[i]:
            if not echec:
                print(f"ÉCHEC : « {texte[max(0, i - 15):i + 15]!r} » : importé {importees[i]}, "
                      f"le relevé dit {sortes[i]}.", file=sys.stderr)
            echec = True
        if sortes[i]:
            soulignes += 1
            vus.add(sortes[i])

if not echec and (len(vus) != 16 or not any(s[1] for s in vus)):
    print(f"ÉCHEC : {len(vus)} sortes importées, seize attendues, dont le double.", file=sys.stderr)
    echec = True
print(f"    {soulignes} caractères soulignés importés chacun dans sa sorte, {len(vus)} sortes, le double compris")
sys.exit(1 if echec else 0)
PY
echo "  4. l'import : un .pub souligné, lu par libmspub, s'importe sorte pour sorte."

# --------------------------------------------- 5. la frappe, la copie et la commande

# « dotnet test » sort en 0 quand AUCUN test ne correspond au filtre : on compte.
for projet in MyPublisher.Tests MyPublisher.Tests.Interface; do
    if ! dotnet test "$projet/$projet.csproj" ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
            --nologo -v q --filter "poste=I7" > "$travail/tests.txt" 2>&1; then
        echo "ÉCHEC : les tests du poste ne passent pas ($projet)." >&2
        sed 's/^/    /' "$travail/tests.txt" >&2
        exit 1
    fi
    joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
    attendus=$([ "$projet" = MyPublisher.Tests ] && echo 58 || echo 7)
    if [ -z "$joues" ] || [ "$joues" -lt "$attendus" ]; then
        echo "ÉCHEC : ${joues:-0} test(s) du poste joué(s) dans $projet, $attendus au moins attendus." >&2
        exit 1
    fi
    echo "  5. $projet : $joues tests du poste joués."
done

echo "I7 : le souligné et ses dix-sept sortes s'écrivent, se dessinent à la place de la police à l'écran et au PDF, s'importent et suivent la frappe."
