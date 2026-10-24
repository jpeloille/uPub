#!/usr/bin/env bash
#
# Recette du poste I1 — la charte de l'interface.
#
# La charte (MyPublisher/Views/Charte.axaml, chargée par App.axaml) est la seule source de
# la palette, des tailles et des icônes de la maquette « Fenêtre principale — bloc de texte
# sélectionné ». Aucune vue ne s'en sert encore : le poste la pose, I2 à I9 la lisent. La
# recette vérifie donc deux choses de nature différente, et la seconde attend son heure.
#
#   1. LES SOURCES, lues par un script. La maquette elle-même est relue, et non une copie
#      de ses valeurs : la charte est comparée à ce qu'elle dessine.
#      a. Chaque couleur de la maquette — moins la page qu'elle montre, qui est le
#         document, et le port de chaînage, que la phase ne fait pas — est déclarée une
#         fois et une seule dans la charte, en « Color » ; la charte n'en déclare pas
#         d'autre, et n'écrit nulle part ailleurs une couleur en chiffres.
#      b. Chaque style de la charte est porté par une classe — aucun sélecteur nu qui
#         rhabillerait la fenêtre d'aujourd'hui —, et aucune de ces classes n'est déjà
#         employée par une vue d'avant la phase.
#      c. Chaque icône de la charte est le tracé de la maquette, élément par élément :
#         géométrie, épaisseur, extrémités, jointures, pointillé, couleur, taille. La
#         table ci-dessous dit où la maquette la dessine et quel poste l'emploiera.
#      d. Chaque icône nommée par une vue existe dans la charte ; chaque icône de la charte
#         sert dans une vue DÈS QUE le poste qui l'attend est clos au tableau de suivi de
#         PLAN.md. En I1, aucun ne l'est : la clause dort, et le dit.
#      e. Aucune vue de la liste ci-dessous — celles que la phase écrit ou réécrit —
#         n'écrit en dur une couleur de la maquette. En I1, la liste est vide.
#      Pour que les clauses qui dorment ne soient pas des clauses mortes, chaque contrôle
#      est aussi joué sur un TÉMOIN fautif — une vue qui écrit l'accent en dur, une icône
#      que rien n'emploie sous un poste clos… — et doit le refuser, à chaque passage.
#   2. LES TESTS d'interface « poste=I1 » : un bouton-icône rend 30 × 30 px à l'échelle 1,
#      plein d'accent quand il est actif, l'icône à la couleur du bouton ; chaque icône se
#      crée à chaque emploi et se dessine dans sa boîte ; champ, libellé, séparateur et
#      titre de section ont les mesures de la maquette ; aucune clé de la charte ne masque
#      une ressource du thème ; et la fenêtre d'aujourd'hui, ouverte sur l'exemple avec un
#      bloc sélectionné, rend le même image au pixel près, avec ou sans la charte.
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

# Les vues que la phase I écrit ou réécrit, chemin depuis la racine du dépôt. Chaque poste
# de la phase qui écrit une vue, ou en réécrit une, l'ajoute ici dans le commit qui la
# pose — I2 la fenêtre, I3 la barre contextuelle, I4 la planche… Une vue de cette liste
# n'écrit aucune couleur de la maquette en chiffres : elle cite la charte (clause e).
vues_de_la_phase=(
)

# Les icônes de la charte : sa clé, le poste qui l'emploiera, et où la maquette la
# dessine — « bouton:X », le svg du bouton dont « aria-label » vaut X ; « svg:X », le svg
# qui porte lui-même cet « aria-label » ; « suivi:X », le svg que suit immédiatement le
# texte X. La maquette n'a pas l'alignement à droite : « miroir:SOURCE:TRACÉ » dit qu'il
# est dessiné comme SOURCE, au tracé près. Une icône ajoutée à la charte s'ajoute ici,
# et réciproquement (clause c). La pipette n'y est pas : la phase ne la fait pas.
icones=(
    "IconeNouveau                 I2   bouton:Nouveau"
    "IconeOuvrir                  I2   bouton:Ouvrir"
    "IconeEnregistrer             I2   bouton:Enregistrer"
    "IconeAnnuler                 I2   bouton:Annuler"
    "IconeRetablir                I2   bouton:Rétablir"
    "IconeAlignerAGauche          I2   bouton:Aligner à gauche"
    "IconeCentrerHorizontalement  I2   bouton:Centrer horizontalement"
    "IconeCentrerVerticalement    I2   bouton:Centrer verticalement"
    "IconeAjusterAuxMarges        I2   bouton:Ajuster aux marges"
    "IconePremierPlan             I2   bouton:Premier plan"
    "IconeArrierePlan             I2   bouton:Arrière-plan"
    "IconeGrouper                 I2   bouton:Grouper"
    "IconeZoomArriere             I2   bouton:Zoom arrière"
    "IconeZoomAvant               I2   bouton:Zoom avant"
    "IconeExporter                I2   suivi:Exporter en PDF"
    "IconeTexteAGauche            I3   bouton:Aligner le texte à gauche"
    "IconeTexteCentre             I3   bouton:Centrer le texte"
    "IconeTexteJustifie           I3   bouton:Justifier"
    "IconeTexteADroite            I3   miroir:IconeTexteAGauche:M4 6h16M10 10h10M4 14h16M10 18h10"
    "IconeSelection               I2   bouton:Sélection (V)"
    "IconeMain                    I6   bouton:Main (Espace)"
    "IconeBlocDeTexte             I2   bouton:Bloc de texte (T)"
    "IconeTitre                   I2   bouton:Titre (H)"
    "IconeImage                   I2   bouton:Image"
    "IconeTableau                 I2   bouton:Tableau (B)"
    "IconeRectangle               I2   bouton:Rectangle (R)"
    "IconeEllipse                 I2   bouton:Ellipse (E)"
    "IconeLigne                   A10  bouton:Ligne (L)"
    "IconeLoupe                   I6   bouton:Zoom (Z)"
    "IconeAjouterPage             I2   bouton:Ajouter une page"
    "IconeSectionOuverte          I2   suivi:Position et taille"
    "IconeSectionFermee           I2   suivi:Habillage"
    "IconeRotation                I2   svg:Rotation"
    "IconeAlerte                  I9   suivi:Texte en excédent"
    "IconeOeil                    I8   svg:Visible"
)

printf '%s\n' "${icones[@]}" > "$travail/icones.txt"
printf '%s\n' ${vues_de_la_phase[@]+"${vues_de_la_phase[@]}"} > "$travail/vues.txt"

# ---------------------------------------------------------------------- 1. les sources

python3 - "$racine" "$travail" <<'PY'
import pathlib, re, sys, xml.etree.ElementTree as ET

racine, travail = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
CHARTE = racine / "MyPublisher/Views/Charte.axaml"
MAQUETTE = racine / "Maquettes/Fenêtre principale — bloc de texte sélectionné-html/Main.dc.html"
PLAN = racine / "MyPublisher/Docs/PLAN.md"
X = "{http://schemas.microsoft.com/winfx/2006/xaml}"
A = "{https://github.com/avaloniaui}"

echecs = []
def refuser(message):
    echecs.append(message)

maquette = MAQUETTE.read_text(encoding="utf-8")
texte_charte = CHARTE.read_text(encoding="utf-8")
charte = ET.fromstring(texte_charte)
vues_de_la_phase = [l for l in (travail / "vues.txt").read_text().splitlines() if l.strip()]

# Les vues : tout ce qui, sous MyPublisher/, décrit ou construit l'interface — la charte
# exceptée, qui déclare ce que les autres citent.
def vues():
    for chemin in sorted((racine / "MyPublisher").rglob("*")):
        if chemin.suffix in (".axaml", ".cs") and chemin != CHARTE \
                and not {"bin", "obj"} & set(chemin.relative_to(racine).parts):
            yield str(chemin.relative_to(racine)), chemin.read_text(encoding="utf-8")

# ------------------------------------------------------------------ a. les couleurs

# Ce que la maquette montre et qui n'est pas l'interface. Chaque exclusion doit y être
# encore : une maquette retouchée ne laisse pas une exclusion morte derrière elle.
HORS_CHARTE = {
    "#1F2A44": "le document — bandeau, titres, filet, couleur du texte et du contour montrés au dock",
    "#C9D6EE": "le document — les images",
    "#F1F4FA": "le document — le fond du pied de page",
    "#C9D3EA": "le document — le sous-titre du bandeau",
    "#3A4E7A": "le document — la légende « [Image] »",
    "#B5BCC6": "le document — les lignes de texte des vignettes",
    "#C2410C": "le port de chaînage, que la phase ne fait pas (seuil D)",
}
HEX = re.compile(r"#(?:[0-9A-Fa-f]{2})?([0-9A-Fa-f]{6})\b")

couleurs_maquette = {"#" + h.upper() for h in re.findall(r"#([0-9A-Fa-f]{6})\b", maquette)}
for couleur in HORS_CHARTE:
    if couleur not in couleurs_maquette:
        refuser(f"couleurs : {couleur} est exclue de la charte mais n'est plus dans la maquette.")
palette = couleurs_maquette - set(HORS_CHARTE)

declarees = {}   # clé → couleur
for e in charte.iter(A + "Color"):
    declarees[e.get(X + "Key")] = "#" + HEX.fullmatch(e.text.strip()).group(1).upper()
for couleur in sorted(palette):
    n = list(declarees.values()).count(couleur)
    if n != 1:
        refuser(f"couleurs : {couleur}, de la maquette, est déclarée {n} fois dans la charte (une attendue).")
for cle, couleur in sorted(declarees.items()):
    if couleur not in palette:
        refuser(f"couleurs : « {cle} » ({couleur}) n'est pas une couleur de l'interface de la maquette.")
ecrites = len(HEX.findall(texte_charte))
if ecrites != len(declarees):
    refuser(f"couleurs : la charte écrit {ecrites} couleurs en chiffres pour {len(declarees)} déclarations — "
            f"un style ou une icône cite une couleur sans passer par sa clé.")

# Les pinceaux, pour lire la couleur d'un trait ou d'un fond d'icône.
pinceaux = {}
for e in charte.iter(A + "SolidColorBrush"):
    m = re.fullmatch(r"\{StaticResource (\w+)\}", e.get("Color", ""))
    if m and m.group(1) in declarees and e.get("Opacity") is None:
        pinceaux[e.get(X + "Key")] = declarees[m.group(1)]

print(f"    a. {len(palette)} couleurs de la maquette, chacune déclarée une fois ; "
      f"{len(HORS_CHARTE)} laissées au document ou hors de la phase")

# ------------------------------------------------------------- b. classes et sélecteurs

def classes_des_selecteurs(selecteurs):
    """Les classes d'une liste de sélecteurs, et ceux qui ne commencent pas par une classe."""
    classes, nus = set(), []
    for selecteur in selecteurs:
        for variante in selecteur.split(","):
            variante = " ".join(variante.split())
            if not re.match(r"^[A-Za-z]+(\.[A-Za-z]\w*)+", variante):
                nus.append(variante)
            classes |= set(re.findall(r"\.([A-Za-z]\w*)", variante))
    return classes, nus

def classes_employees(textes):
    """Les classes que des vues posent, en XAML ou en C#, avec la vue qui les pose."""
    employees = {}
    for nom, texte in textes:
        for groupe in re.findall(r'Classes="([^"]*)"', texte):
            for c in groupe.split():
                employees.setdefault(c, nom)
        for groupe in re.findall(r'Classes\s*=\s*\{([^}]*)\}', texte):
            for c in re.findall(r'"([^"]+)"', groupe):
                employees.setdefault(c, nom)
        for c in re.findall(r'Classes\.(?:Add|Set)\(\s*"([^"]+)"', texte):
            employees.setdefault(c, nom)
    return employees

def controler_styles(selecteurs, textes, exceptees):
    fautes = []
    classes, nus = classes_des_selecteurs(selecteurs)
    for variante in nus:
        fautes.append(f"styles : « {variante} » n'est pas porté par une classe — il rhabillerait la fenêtre d'avant la phase.")
    employees = classes_employees((n, t) for n, t in textes if n not in exceptees)
    for c in sorted(classes & set(employees)):
        fautes.append(f"styles : la classe « {c} » de la charte est déjà employée par {employees[c]}, d'avant la phase.")
    return classes, fautes

selecteurs = [e.get("Selector") for e in charte.iter(A + "Style")]
toutes_les_vues = list(vues())
classes, fautes = controler_styles(selecteurs, toutes_les_vues, set(vues_de_la_phase))
for f in fautes:
    refuser(f)

# Les témoins : un sélecteur nu, une classe déjà prise.
_, fautes = controler_styles(["Button", "TextBox.champ"], [("Témoin.axaml", '<Button Classes="champ"/>')], set())
if len(fautes) != 2:
    refuser(f"témoin : le contrôle des styles laisse passer un sélecteur nu ou une classe prise ({fautes}).")

print(f"    b. {len(selecteurs)} styles, tous portés par une classe ({', '.join(sorted(classes))}) "
      f"qu'aucune vue d'avant la phase n'emploie")

# ---------------------------------------------------------------------- c. les icônes

def nombre(v):
    return f"{float(v):g}"

def rectangle(x, y, l, h, r=0):
    x, y, l, h, r = map(float, (x, y, l, h, r))
    n = nombre
    if r == 0:
        return f"M{n(x)},{n(y)} H{n(x+l)} V{n(y+h)} H{n(x)} Z"
    a = f"A{n(r)},{n(r)} 0 0 1"
    return (f"M{n(x+r)},{n(y)} H{n(x+l-r)} {a} {n(x+l)},{n(y+r)} V{n(y+h-r)} {a} {n(x+l-r)},{n(y+h)} "
            f"H{n(x+r)} {a} {n(x)},{n(y+h-r)} V{n(y+r)} {a} {n(x+r)},{n(y)} Z")

def ellipse(cx, cy, rx, ry):
    cx, cy, rx, ry = map(float, (cx, cy, rx, ry))
    n = nombre
    return (f"M{n(cx+rx)},{n(cy)} A{n(rx)},{n(ry)} 0 1 1 {n(cx-rx)},{n(cy)} "
            f"A{n(rx)},{n(ry)} 0 1 1 {n(cx+rx)},{n(cy)} Z")

HERITES = {"fill": "black", "stroke": "none", "stroke-width": "1", "stroke-linecap": "butt",
           "stroke-linejoin": "miter", "stroke-dasharray": "none"}
EXTREMITES = {"butt": "Flat", "round": "Round", "square": "Square"}
JOINTURES = {"miter": "Miter", "round": "Round", "bevel": "Bevel"}

def svg(localisateur):
    sorte, _, valeur = localisateur.partition(":")
    trouves = []
    for m in re.finditer(r"<svg\b[^>]*>.*?</svg>", maquette, re.S):
        bloc = m.group(0)
        if sorte == "svg" and f'aria-label="{valeur}"' in bloc[:bloc.index(">")]:
            trouves.append(bloc)
        elif sorte == "bouton":
            debut = maquette.rfind("<button", 0, m.start())
            if debut >= 0 and re.fullmatch(r'<button[^>]*aria-label="' + re.escape(valeur) + r'"[^>]*>\s*',
                                           maquette[debut:m.start()]):
                trouves.append(bloc)
        elif sorte == "suivi" and maquette[m.end():].lstrip().startswith(valeur):
            trouves.append(bloc)
    return trouves

def attendu(bloc):
    """Ce que la charte doit porter pour ce svg : sa taille, puis un trait par élément."""
    s = ET.fromstring(bloc)
    base = {k: s.get(k, d) for k, d in HERITES.items()}
    elements = []
    for e in s:
        a = dict(base, **{k: e.get(k) for k in HERITES if e.get(k) is not None})
        if e.tag == "path":
            geometrie = " ".join(e.get("d").split())
        elif e.tag == "rect":
            geometrie = rectangle(e.get("x"), e.get("y"), e.get("width"), e.get("height"), e.get("rx", 0))
        elif e.tag == "circle":
            geometrie = ellipse(e.get("cx"), e.get("cy"), e.get("r"), e.get("r"))
        elif e.tag == "ellipse":
            geometrie = ellipse(e.get("cx"), e.get("cy"), e.get("rx"), e.get("ry"))
        else:
            raise SystemExit(f"élément de svg inattendu dans la maquette : {e.tag}")
        trait = a["stroke"] != "none"
        tirets = a["stroke-dasharray"] if a["stroke-dasharray"] not in ("none", "0") else None
        elements.append({
            "géométrie": geometrie,
            "trait": "courante" if a["stroke"] == "currentColor" else (a["stroke"].upper() if trait else None),
            "fond": "courante" if a["fill"] == "currentColor" else (None if a["fill"] == "none" else a["fill"].upper()),
            "épaisseur": float(a["stroke-width"]) if trait else None,
            "extrémités": EXTREMITES[a["stroke-linecap"]] if trait else None,
            "jointures": JOINTURES[a["stroke-linejoin"]] if trait else None,
            "tirets": [float(t) for t in tirets.split()] if trait and tirets else None,
        })
    return float(s.get("width")), elements

def porte(icone):
    """Ce que la charte porte pour cette icône, dans les mêmes termes."""
    boite = icone
    toile = boite.find(A + "Canvas")
    elements = []
    for p in (toile if toile is not None else []):
        classes = (p.get("Classes") or "").split()
        def couleur(attribut):
            m = re.fullmatch(r"\{StaticResource (\w+)\}", p.get(attribut) or "")
            return pinceaux.get(m.group(1), "?") if m else None
        trait = "courante" if "trait" in classes and p.get("Stroke") is None else couleur("Stroke")
        fond = "courante" if "plein" in classes and p.get("Fill") is None else couleur("Fill")
        epaisseur = float(p.get("StrokeThickness")) if trait and p.get("StrokeThickness") else None
        tirets = p.get("StrokeDashArray")
        elements.append({
            "géométrie": " ".join((p.get("Data") or "").split()),
            "trait": trait,
            "fond": fond,
            "épaisseur": epaisseur,
            "extrémités": (p.get("StrokeLineCap") or "Flat") if trait else None,
            "jointures": (p.get("StrokeJoin") or "Miter") if trait else None,
            # Avalonia compte le pointillé en épaisseurs de trait, le svg en unités.
            "tirets": [round(float(t) * (epaisseur or 1), 3) for t in re.split(r"[ ,]+", tirets.strip())]
                      if trait and tirets else None,
        })
    grille = toile is not None and toile.get("Width") == "24" and toile.get("Height") == "24" \
        and toile.get("ClipToBounds") == "True"
    return boite.get("Width"), boite.get("Height"), boite.get(X + "Shared"), grille, elements

def comparer(cle, taille, elements, icone):
    fautes = []
    largeur, hauteur, partagee, grille, portes = porte(icone)
    if partagee != "False":
        fautes.append(f"{cle} n'est pas déclarée x:Shared=\"False\" : employée deux fois, elle quitterait sa première place.")
    if not grille:
        fautes.append(f"{cle} n'est pas dessinée sur une grille de 24 rognée à ses bords, comme le svg.")
    if (largeur, hauteur) != (nombre(taille), nombre(taille)):
        fautes.append(f"{cle} a une boîte de {largeur} × {hauteur}, la maquette l'affiche en {nombre(taille)}.")
    if len(portes) != len(elements):
        fautes.append(f"{cle} a {len(portes)} tracés, la maquette {len(elements)}.")
    for rang, (a, p) in enumerate(zip(elements, portes), 1):
        for cle_attribut in a:
            va, vp = a[cle_attribut], p[cle_attribut]
            if cle_attribut == "tirets" and va and vp:
                egal = len(va) == len(vp) and all(abs(x - y) < 1e-3 for x, y in zip(va, vp))
            else:
                egal = va == vp
            if not egal:
                fautes.append(f"{cle}, tracé {rang} : {cle_attribut} « {vp} » dans la charte, « {va} » dans la maquette.")
    return fautes

table = []
for ligne in (travail / "icones.txt").read_text(encoding="utf-8").splitlines():
    if ligne.strip():
        cle, poste, localisateur = ligne.split(None, 2)
        table.append((cle, poste, localisateur))

icones_charte = {e.get(X + "Key"): e for e in charte.find(A + "Styles.Resources")
                 if (e.get(X + "Key") or "").startswith("Icone")}
cles_table = [c for c, _, _ in table]
for cle in sorted(set(icones_charte) - set(cles_table)):
    refuser(f"icônes : {cle} est dans la charte mais pas dans la table de la recette (quel poste l'emploie ?).")
for cle in sorted(set(cles_table) - set(icones_charte)):
    refuser(f"icônes : {cle} est dans la table de la recette mais pas dans la charte.")

attendus = {}
for cle, poste, localisateur in table:
    if localisateur.startswith("miroir:"):
        continue
    trouves = svg(localisateur)
    # Plusieurs svg au même repère — les chevrons des sections — doivent être le même.
    if not trouves or any(t != trouves[0] for t in trouves):
        refuser(f"icônes : {cle} — « {localisateur} » désigne {len(trouves)} svg différents dans la maquette.")
        continue
    attendus[cle] = attendu(trouves[0])
for cle, poste, localisateur in table:
    if localisateur.startswith("miroir:"):
        _, source, trace = localisateur.split(":", 2)
        if source in attendus:
            taille, elements = attendus[source]
            attendus[cle] = (taille, [dict(e, **{"géométrie": trace}) for e in elements[:1]])
for cle, (taille, elements) in attendus.items():
    if cle in icones_charte:
        for f in comparer(cle, taille, elements, icones_charte[cle]):
            refuser("icônes : " + f)

# Le témoin : une icône dont un seul chiffre a bougé.
if "IconeNouveau" in attendus:
    taille, elements = attendus["IconeNouveau"]
    temoin = ET.fromstring(ET.tostring(icones_charte["IconeNouveau"]))
    trace = temoin.find(A + "Canvas")[0]
    trace.set("Data", trace.get("Data").replace("14", "15", 1))
    if not comparer("IconeNouveau", taille, elements, temoin):
        refuser("témoin : une icône dont le tracé s'écarte de la maquette passe la comparaison.")

print(f"    c. {len(icones_charte)} icônes, chacune au tracé, au trait et à la taille de la maquette "
      f"({len(attendus) - 1} relevées dans ses svg, une dessinée en miroir)")

# ------------------------------------------------- d. icônes nommées, icônes qui servent

def postes_clos():
    clos, dedans = set(), False
    for ligne in PLAN.read_text(encoding="utf-8").splitlines():
        if ligne.startswith("## Suivi"):
            dedans = True
        elif dedans and ligne.startswith("---"):
            break
        elif dedans:
            m = re.match(r"^\| \*\*(\w+)\*\* \|[^|]*\|([^|]*)\|", ligne)
            if m and m.group(2).replace("**", "").strip() == "100 %":
                clos.add(m.group(1))
    return clos

def controler_emplois(table, textes, clos, charte_cles):
    fautes, dormantes = [], []
    nommees = {}
    for nom, texte in textes:
        for cle in re.findall(r"\bIcone[A-Z]\w*", texte):
            nommees.setdefault(cle, nom)
    for cle, nom in sorted(nommees.items()):
        if cle not in charte_cles:
            fautes.append(f"icônes : {nom} nomme {cle}, que la charte n'a pas.")
    for cle, poste, _ in table:
        if cle in nommees:
            continue
        if poste in clos:
            fautes.append(f"icônes : {cle} ne sert dans aucune vue, alors que {poste}, qui l'emploie, est clos.")
        else:
            dormantes.append(poste)
    return fautes, nommees, dormantes

clos = postes_clos()
if "P1" not in clos:
    refuser("plan : le tableau de suivi ne se lit plus (P1 n'y est pas clos).")
fautes, nommees, dormantes = controler_emplois(table, toutes_les_vues, clos, set(icones_charte))
for f in fautes:
    refuser(f)

# Les témoins : une vue qui nomme une icône absente ; une icône d'un poste clos que rien
# n'emploie. Sans eux, une clause qui dort ne se distinguerait pas d'une clause morte.
fautes, _, _ = controler_emplois([("IconeTemoin", "P1", "")],
                                 [("Témoin.axaml", '<Button Content="{StaticResource IconeInexistante}"/>')],
                                 clos, set(icones_charte))
if len(fautes) != 2:
    refuser(f"témoin : le contrôle des emplois laisse passer une icône absente ou inemployée ({fautes}).")

servent = sum(1 for c, _, _ in table if c in nommees)
attente = ", ".join(f"{p} ({dormantes.count(p)})" for p in sorted(set(dormantes)))
print(f"    d. {len(nommees)} icône(s) nommée(s) par une vue, toutes dans la charte ; {servent} servent ; "
      f"la clause dort pour {len(dormantes)}, dont le poste est ouvert : {attente or 'aucune'}")

# ------------------------------------------------------ e. les vues de la phase

def couleurs_en_dur(nom, texte):
    fautes = []
    for numero, ligne in enumerate(texte.splitlines(), 1):
        for m in HEX.finditer(ligne):
            if "#" + m.group(1).upper() in palette:
                fautes.append(f"vues : {nom}:{numero} écrit {m.group(0)}, une couleur de la maquette — "
                              f"la charte la nomme.")
    return fautes

for nom in vues_de_la_phase:
    chemin = racine / nom
    if not chemin.is_file():
        refuser(f"vues : {nom}, dans la liste des vues de la phase, n'existe pas.")
        continue
    for f in couleurs_en_dur(nom, chemin.read_text(encoding="utf-8")):
        refuser(f)

if len(couleurs_en_dur("Témoin.axaml", '<Border Background="#ff2557c7" BorderBrush="#E2E5EA"/>')) != 2:
    refuser("témoin : une vue qui écrit l'accent et un filet en chiffres passe le contrôle.")

print(f"    e. {len(vues_de_la_phase)} vue(s) de la phase, aucune n'écrit une couleur de la maquette "
      f"(témoin fautif refusé)")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : couleurs, classes, icônes, emplois et vues de la phase"

# ------------------------------------------------------------------------ 2. les tests

attendus=5
if ! dotnet test MyPublisher.Tests.Interface/MyPublisher.Tests.Interface.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=I1" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les tests d'interface sous « poste=I1 » ne passent pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=I1 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  2. la fenêtre : bouton-icône de 30 × 30 plein d'accent, icônes, champs, thème intact, écran d'avant inchangé ($joues tests)"

echo "I1 : la charte porte la palette, les classes et les icônes de la maquette, sans rien changer à l'écran d'aujourd'hui."
