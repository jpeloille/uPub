#!/usr/bin/env bash
#
# Recette du poste I1 — l'apparence de l'interface.
#
# L'apparence (MyPublisher/Views/Apparence.axaml, chargée par App.axaml) est la seule source
# de la palette, des tailles et des icônes de la maquette « Fenêtre principale — bloc de
# texte sélectionné ». Elle ne s'appelle pas « charte » : le mot nomme déjà, dans le noyau,
# le jeu de couleurs et de polices du document. Aucune vue ne s'en sert encore : le poste
# la pose, I2 à I9 la lisent. La recette vérifie donc deux choses de nature différente, et
# une part de la première attend son heure.
#
#   1. LES SOURCES, lues par un script. La maquette elle-même est relue, et non une copie
#      de ses valeurs : l'apparence est comparée à ce qu'elle dessine.
#      a. Chaque couleur de la maquette — moins la page qu'elle montre, qui est le
#         document, et le port de chaînage, que la phase ne fait pas — est déclarée une
#         fois et une seule, en « Color », sous la clé que lui donne la table ci-dessous ;
#         l'apparence n'en déclare pas d'autre, et n'écrit nulle part ailleurs une couleur
#         en dur, ni en chiffres sous aucune forme (#FFF comme #FF2557C7) ni par son nom
#         (« White ») ; chaque transparence de la maquette prise dans sa palette — l'accent
#         à 14 % — est un pinceau de la même opacité.
#      b. Chaque style est porté par une classe — aucun sélecteur nu qui rhabillerait la
#         fenêtre d'aujourd'hui —, qu'aucune vue hors de la phase n'emploie ; et chaque
#         classe que la feuille de style de la maquette décrit (.ib, .ib:hover, .ib.on,
#         .fld, .lbl, .sep, .sec) a ici son style, aux mêmes tailles et aux mêmes couleurs,
#         chacune citée par une clé qui la porte.
#      c. Chaque icône est le tracé de la maquette, élément par élément : géométrie,
#         épaisseur, extrémités, jointures, pointillé, couleur, taille. La table ci-dessous
#         dit où la maquette la dessine et quel poste l'emploiera ; l'alignement à droite,
#         que la maquette omet, est le miroir calculé de l'alignement à gauche.
#      d. Chaque icône qu'une vue emploie existe dans l'apparence ; chaque icône de
#         l'apparence sert dans une vue DÈS QUE le poste qui l'attend est clos au tableau
#         de suivi de PLAN.md. Seul compte un emploi — « {StaticResource IconeX} », la clé
#         passée à une recherche de ressource —, pas un commentaire qui la nomme.
#      e. Aucune vue de la phase — la liste ci-dessous — n'écrit en dur une couleur de la
#         maquette, sous aucune forme. Et la liste ne se tient pas de mémoire : une vue qui
#         cite l'apparence sans y être est refusée, et hors de la liste, les couleurs de la
#         maquette écrites en dur sont exactement celles du relevé d'avant la phase. Une
#         planche qui prend en chiffres le gris de la maquette sans entrer dans la liste
#         change ce relevé : elle est refusée.
#      Pour que les clauses qui dorment ne soient pas des clauses mortes, chaque contrôle
#      est aussi joué sur un TÉMOIN fautif — une vue qui écrit l'accent en dur, une vue hors
#      de la liste qui cite un filet, une icône que rien n'emploie sous un poste clos… — et
#      doit le refuser, à chaque passage.
#   2. LES TESTS d'interface « poste=I1 » : un bouton-icône rend 30 × 30 px à l'échelle 1,
#      à rayon 6, plein d'accent quand il est actif, l'icône à la couleur du bouton ;
#      survolé ou pressé, il prend le gris de la maquette, actif il reste plein d'accent,
#      grisé il s'efface sans fond ; chaque icône se crée à chaque emploi et se dessine dans
#      sa boîte ; champ, libellé, séparateur et titre de section ont les mesures de la
#      maquette ; aucune clé de l'apparence ne masque une ressource du thème ; et les vues
#      d'avant la phase — la planche ouverte sur l'exemple avec un bloc sélectionné, et les
#      deux dialogues — rendent la même image au pixel près, avec ou sans l'apparence. La
#      fenêtre principale en était jusqu'au poste I2, qui la refait sur l'apparence.
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
# n'écrit aucune couleur de la maquette en dur : elle cite l'apparence (clause e). L'oubli
# ne passe pas : une vue qui cite l'apparence, ou qui s'écarte du relevé ci-dessous, sans
# être ici, fait échouer la recette.
vues_de_la_phase=(
    MyPublisher/Views/MainWindow.axaml
    MyPublisher/Views/BarreContextuelle.axaml
    MyPublisher/Views/BoutonDEtat.cs
    MyPublisher/Views/EntreeDePage.axaml
    MyPublisher/Views/ChampDuZoom.axaml
    MyPublisher/Views/PageCanvas.axaml
    MyPublisher/Views/PageCanvas.axaml.cs
    MyPublisher/Views/PageCanvas.Planche.cs
    MyPublisher/Views/Ruler.cs
    MyPublisher/Views/PageCanvas.Navigation.cs
    MyPublisher/Views/VignetteDePage.cs
    MyPublisher/Views/TraitBloc.cs
    MyPublisher/Views/PageCanvas.Trait.cs
)

# Le relevé d'avant la phase : les couleurs de la maquette que des fichiers hors de la
# liste écrivent en dur aujourd'hui — « fichier couleur nombre », la couleur ramenée à
# #RRGGBB quelle que soit sa forme (#FFFAFAFB, « White », « Brushes.White »). C'est le gris
# des panneaux de la fenêtre d'avant la phase, et le blanc de la fenêtre, du marqueur de
# débordement et des vignettes de modèle. Hors de la liste, une vue écrit exactement ce que
# dit le relevé : ni une couleur de plus — ce serait une vue de la phase qui ne le dit
# pas —, ni une de moins, qu'un relevé périmé laisserait reprendre en silence. Une vue qui
# entre dans la liste en sort : la planche y est entrée avec I4, et le blanc de sa page —
# celui du papier, que le document nomme (Document.Papier) — a quitté le relevé.
releve=(
    "MyPublisher/Views/MarqueurDebordement.cs    #FFFFFF  1"
    "MyPublisher/Views/TemplatePreview.cs        #FFFFFF  1"
)

# Le nom de chaque couleur. La feuille de style de la maquette ne dit le rôle que de
# quelques-unes, et la clause b vérifie au style près les neuf que portent les classes de
# l'apparence ; pour les autres, c'est le rôle que la maquette leur donne dans son dessin,
# tenu ici et non dans l'apparence, qui ne peut pas être à la fois ce qu'on mesure et
# l'étalon. Sans cette table, deux couleurs échangées — l'accent et son survol — restaient
# chacune déclarée une fois, et passaient.
couleurs=(
    "CouleurAccent                 #2557C7"
    "CouleurAccentSurvol           #1B439C"
    "CouleurTexte                  #1F2329"
    "CouleurCommande               #2B323B"
    "CouleurLibelle                #5B6470"
    "CouleurEtat                   #4A525D"
    "CouleurBlanc                  #FFFFFF"
    "CouleurFondFenetre            #F4F5F7"
    "CouleurFondPanneau            #FAFAFB"
    "CouleurFilet                  #E2E5EA"
    "CouleurFiletFort              #D5D9DF"
    "CouleurBordChamp              #CDD2D9"
    "CouleurSurvol                 #E6E9EE"
    "CouleurFondBarreContextuelle  #EEF2FB"
    "CouleurFiletBarreContextuelle #D6DEEE"
    "CouleurPastille               #DCE5F8"
    "CouleurFondAlerte             #FDF1E8"
    "CouleurTexteAlerte            #8A2E07"
    "CouleurFondInitiale           #EEF0F3"
    "CouleurTexteInitiale          #3A424D"
    "CouleurGraduation             #9AA2AD"
    "CouleurGraduationFine         #C3C8D0"
    "CouleurFondPlanche            #DCDFE4"
    "CouleurRepere                 #12A4C4"
    "CouleurMargeDePage            #E3A6D6"
    "CouleurAucun                  #D23B3B"
)

# Les icônes de l'apparence : sa clé, le poste qui l'emploiera, et où la maquette la
# dessine — « bouton:X », le svg du bouton dont « aria-label » vaut X ; « svg:X », le svg
# qui porte lui-même cet « aria-label » ; « suivi:X », le svg que suit immédiatement le
# texte X. La maquette n'a pas l'alignement à droite : « miroir:SOURCE » dit qu'il est le
# reflet de SOURCE par x → 24 − x, que la recette calcule. Une icône ajoutée à l'apparence
# s'ajoute ici, et réciproquement (clause c). La pipette n'y est pas : la phase ne la fait
# pas.
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
    "IconeTexteADroite            I3   miroir:IconeTexteAGauche"
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
printf '%s\n' "${couleurs[@]}" > "$travail/couleurs.txt"
printf '%s\n' "${releve[@]}" > "$travail/releve.txt"
printf '%s\n' ${vues_de_la_phase[@]+"${vues_de_la_phase[@]}"} > "$travail/vues.txt"

# ---------------------------------------------------------------------- 1. les sources

python3 - "$racine" "$travail" <<'PY'
import collections, pathlib, re, sys, xml.etree.ElementTree as ET

racine, travail = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
APPARENCE = racine / "MyPublisher/Views/Apparence.axaml"
MAQUETTE = racine / "Maquettes/Fenêtre principale — bloc de texte sélectionné-html/Main.dc.html"
PLAN = racine / "MyPublisher/Docs/PLAN.md"
X = "{http://schemas.microsoft.com/winfx/2006/xaml}"
A = "{https://github.com/avaloniaui}"

echecs = []
def refuser(message):
    echecs.append(message)

def bilan(clause, debut, message):
    """Le bilan d'une clause : elle ne se dit satisfaite que si elle n'a rien refusé."""
    fautes = len(echecs) - debut
    print(f"    {clause}. " + (message if not fautes else f"{fautes} faute(s), dites plus bas"))

def table(nom):
    return [l.split() for l in (travail / nom).read_text(encoding="utf-8").splitlines() if l.strip()]

maquette = MAQUETTE.read_text(encoding="utf-8")
apparence = ET.fromstring(APPARENCE.read_text(encoding="utf-8"))
vues_de_la_phase = [l[0] for l in table("vues.txt")]

# Les vues : tout ce qui, sous MyPublisher/, décrit ou construit l'interface — l'apparence
# exceptée, qui déclare ce que les autres citent.
def vues():
    for chemin in sorted((racine / "MyPublisher").rglob("*")):
        if chemin.suffix in (".axaml", ".cs") and chemin != APPARENCE \
                and not {"bin", "obj"} & set(chemin.relative_to(racine).parts):
            yield str(chemin.relative_to(racine)), chemin.read_text(encoding="utf-8")

toutes_les_vues = list(vues())

# ------------------------------------------------------ les couleurs écrites en dur

# Les couleurs nommées d'Avalonia 11.3.9 — les propriétés statiques de « Colors »,
# relevées par réflexion. Le XAML les lit sans égard à la casse : « white » vaut #FFFFFF.
NOMS = {}
for entree in """
    AliceBlue:FFF0F8FF AntiqueWhite:FFFAEBD7 Aqua:FF00FFFF Aquamarine:FF7FFFD4
    Azure:FFF0FFFF Beige:FFF5F5DC Bisque:FFFFE4C4 Black:FF000000 BlanchedAlmond:FFFFEBCD
    Blue:FF0000FF BlueViolet:FF8A2BE2 Brown:FFA52A2A BurlyWood:FFDEB887 CadetBlue:FF5F9EA0
    Chartreuse:FF7FFF00 Chocolate:FFD2691E Coral:FFFF7F50 CornflowerBlue:FF6495ED
    Cornsilk:FFFFF8DC Crimson:FFDC143C Cyan:FF00FFFF DarkBlue:FF00008B DarkCyan:FF008B8B
    DarkGoldenrod:FFB8860B DarkGray:FFA9A9A9 DarkGreen:FF006400 DarkKhaki:FFBDB76B
    DarkMagenta:FF8B008B DarkOliveGreen:FF556B2F DarkOrange:FFFF8C00 DarkOrchid:FF9932CC
    DarkRed:FF8B0000 DarkSalmon:FFE9967A DarkSeaGreen:FF8FBC8F DarkSlateBlue:FF483D8B
    DarkSlateGray:FF2F4F4F DarkTurquoise:FF00CED1 DarkViolet:FF9400D3 DeepPink:FFFF1493
    DeepSkyBlue:FF00BFFF DimGray:FF696969 DodgerBlue:FF1E90FF Firebrick:FFB22222
    FloralWhite:FFFFFAF0 ForestGreen:FF228B22 Fuchsia:FFFF00FF Gainsboro:FFDCDCDC
    GhostWhite:FFF8F8FF Gold:FFFFD700 Goldenrod:FFDAA520 Gray:FF808080 Green:FF008000
    GreenYellow:FFADFF2F Honeydew:FFF0FFF0 HotPink:FFFF69B4 IndianRed:FFCD5C5C
    Indigo:FF4B0082 Ivory:FFFFFFF0 Khaki:FFF0E68C Lavender:FFE6E6FA LavenderBlush:FFFFF0F5
    LawnGreen:FF7CFC00 LemonChiffon:FFFFFACD LightBlue:FFADD8E6 LightCoral:FFF08080
    LightCyan:FFE0FFFF LightGoldenrodYellow:FFFAFAD2 LightGray:FFD3D3D3 LightGreen:FF90EE90
    LightPink:FFFFB6C1 LightSalmon:FFFFA07A LightSeaGreen:FF20B2AA LightSkyBlue:FF87CEFA
    LightSlateGray:FF778899 LightSteelBlue:FFB0C4DE LightYellow:FFFFFFE0 Lime:FF00FF00
    LimeGreen:FF32CD32 Linen:FFFAF0E6 Magenta:FFFF00FF Maroon:FF800000
    MediumAquamarine:FF66CDAA MediumBlue:FF0000CD MediumOrchid:FFBA55D3
    MediumPurple:FF9370DB MediumSeaGreen:FF3CB371 MediumSlateBlue:FF7B68EE
    MediumSpringGreen:FF00FA9A MediumTurquoise:FF48D1CC MediumVioletRed:FFC71585
    MidnightBlue:FF191970 MintCream:FFF5FFFA MistyRose:FFFFE4E1 Moccasin:FFFFE4B5
    NavajoWhite:FFFFDEAD Navy:FF000080 OldLace:FFFDF5E6 Olive:FF808000 OliveDrab:FF6B8E23
    Orange:FFFFA500 OrangeRed:FFFF4500 Orchid:FFDA70D6 PaleGoldenrod:FFEEE8AA
    PaleGreen:FF98FB98 PaleTurquoise:FFAFEEEE PaleVioletRed:FFDB7093 PapayaWhip:FFFFEFD5
    PeachPuff:FFFFDAB9 Peru:FFCD853F Pink:FFFFC0CB Plum:FFDDA0DD PowderBlue:FFB0E0E6
    Purple:FF800080 Red:FFFF0000 RosyBrown:FFBC8F8F RoyalBlue:FF4169E1 SaddleBrown:FF8B4513
    Salmon:FFFA8072 SandyBrown:FFF4A460 SeaGreen:FF2E8B57 SeaShell:FFFFF5EE Sienna:FFA0522D
    Silver:FFC0C0C0 SkyBlue:FF87CEEB SlateBlue:FF6A5ACD SlateGray:FF708090 Snow:FFFFFAFA
    SpringGreen:FF00FF7F SteelBlue:FF4682B4 Tan:FFD2B48C Teal:FF008080 Thistle:FFD8BFD8
    Tomato:FFFF6347 Transparent:00FFFFFF Turquoise:FF40E0D0 Violet:FFEE82EE Wheat:FFF5DEB3
    White:FFFFFFFF WhiteSmoke:FFF5F5F5 Yellow:FFFFFF00 YellowGreen:FF9ACD32
""".split():
    nom, argb = entree.split(":")
    NOMS[nom.lower()] = (int(argb[:2], 16), "#" + argb[2:])

# Une couleur en chiffres, sous les quatre formes qu'Avalonia lit : #RGB, #ARGB, #RRGGBB et
# #AARRGGBB. Le « \b » final écarte un nom d'élément (« Border#Background ») : la lettre
# qui suit n'y est pas un chiffre hexadécimal.
HEX = re.compile(r"#([0-9A-Fa-f]{8}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{4}|[0-9A-Fa-f]{3})\b")
# Une couleur par son nom : la valeur entière d'un attribut (« Background="White" »), une
# propriété de « Colors » ou de « Brushes », la chaîne passée à « Parse ».
NOMMEE = re.compile(r'="\s*([A-Za-z]+)\s*"|\b(?:Colors|Brushes)\.([A-Za-z]+)\b|Parse\(\s*"([A-Za-z]+)"')
# Une couleur bâtie en C# sur ses composantes écrites en chiffres.
OCTET = r"\s*(0[xX][0-9A-Fa-f]+|\d+)\s*"
COMPOSANTES = re.compile(rf"Color\.FromRgb\({OCTET},{OCTET},{OCTET}\)"
                         rf"|Color\.FromArgb\({OCTET},{OCTET},{OCTET},{OCTET}\)"
                         rf"|Color\.FromUInt32\({OCTET}\)")

def lire_hex(chiffres):
    """(alpha, « #RRGGBB ») d'une couleur écrite en chiffres, sous l'une des quatre formes."""
    if len(chiffres) <= 4:
        chiffres = "".join(c * 2 for c in chiffres)
    if len(chiffres) == 6:
        chiffres = "FF" + chiffres
    return int(chiffres[:2], 16), "#" + chiffres[2:].upper()

def couleurs_en_dur(texte):
    """Chaque couleur écrite en dur dans ce texte : (ligne, ce qui est écrit, alpha, #RRGGBB)."""
    trouvees = []
    for numero, ligne in enumerate(texte.splitlines(), 1):
        for m in HEX.finditer(ligne):
            trouvees.append((numero, m.group(0), *lire_hex(m.group(1))))
        for m in NOMMEE.finditer(ligne):
            nom = next(g for g in m.groups() if g)
            if nom.lower() in NOMS:
                trouvees.append((numero, nom, *NOMS[nom.lower()]))
        for m in COMPOSANTES.finditer(ligne):
            v = [int(g, 16) if g.lower().startswith("0x") else int(g) for g in m.groups() if g]
            if len(v) == 1:
                v = [(v[0] >> decalage) & 255 for decalage in (24, 16, 8, 0)]
            elif len(v) == 3:
                v = [255] + v
            trouvees.append((numero, m.group(0), v[0], "#%02X%02X%02X" % tuple(v[1:])))
    return trouvees

# ------------------------------------------------------------------ a. les couleurs

debut = len(echecs)
# Ce que la maquette montre et qui n'est pas l'interface. Chaque exclusion doit y être
# encore : une maquette retouchée ne laisse pas une exclusion morte derrière elle.
HORS_INTERFACE = {
    "#1F2A44": "le document — bandeau, titres, filet, couleur du texte et du contour montrés au dock",
    "#C9D6EE": "le document — les images",
    "#F1F4FA": "le document — le fond du pied de page",
    "#C9D3EA": "le document — le sous-titre du bandeau",
    "#3A4E7A": "le document — la légende « [Image] »",
    "#B5BCC6": "le document — les lignes de texte des vignettes",
    "#C2410C": "le port de chaînage, que la phase ne fait pas (seuil D)",
}

couleurs_maquette = {lire_hex(m.group(1))[1] for m in HEX.finditer(maquette)}
for couleur in HORS_INTERFACE:
    if couleur not in couleurs_maquette:
        refuser(f"couleurs : {couleur} est exclue de l'apparence mais n'est plus dans la maquette.")
palette = couleurs_maquette - set(HORS_INTERFACE)

def de_la_palette(alpha, couleur):
    """Une couleur de la maquette — le transparent, fût-il blanc en chiffres, n'en est pas une."""
    return alpha != 0 and couleur in palette

declarees = {}   # clé → couleur
for e in apparence.iter(A + "Color"):
    cle, texte = e.get(X + "Key"), (e.text or "").strip()
    if not re.fullmatch(r"#[0-9A-Fa-f]{6}", texte):
        refuser(f"couleurs : « {cle} » s'écrit « {texte} » ; une déclaration s'écrit #RRGGBB, comme la maquette.")
        continue
    declarees[cle] = texte.upper()
for couleur in sorted(palette):
    n = list(declarees.values()).count(couleur)
    if n != 1:
        refuser(f"couleurs : {couleur}, de la maquette, est déclarée {n} fois dans l'apparence (une attendue).")
for cle, couleur in sorted(declarees.items()):
    if couleur not in palette:
        refuser(f"couleurs : « {cle} » ({couleur}) n'est pas une couleur de l'interface de la maquette.")

# Chaque couleur sous sa clé.
nommees = {cle: couleur for cle, couleur in table("couleurs.txt")}
for cle in sorted(set(nommees) | set(declarees)):
    if cle not in declarees:
        refuser(f"couleurs : « {cle} » ({nommees[cle]}), de la table de la recette, manque à l'apparence.")
    elif cle not in nommees:
        refuser(f"couleurs : « {cle} » n'est pas dans la table de la recette — quelle couleur de la maquette nomme-t-elle ?")
    elif nommees[cle] != declarees[cle]:
        refuser(f"couleurs : « {cle} » vaut {declarees[cle]} dans l'apparence, {nommees[cle]} dans la table "
                f"de la recette — deux couleurs échangées ?")

# Nulle part ailleurs une couleur en dur : ni dans un pinceau, ni dans un style, ni dans
# une icône. Les sélecteurs sont lus à part — « ContentPresenter#PART_… » n'est pas une
# couleur —, et les commentaires, que le XML ne garde pas, ne comptent pas.
for e in apparence.iter():
    valeurs = [(nom, v) for nom, v in e.attrib.items() if nom not in ("Selector", X + "Key")]
    if e.tag != A + "Color" and (e.text or "").strip():
        valeurs.append(("texte", e.text.strip()))
    for nom, valeur in valeurs:
        for _, ecrit, alpha, _ in couleurs_en_dur(f'x="{valeur}"'):
            if ecrit.lower() != "transparent":
                ou = e.get("Property") or e.get(X + "Key") or e.tag.split("}")[-1]
                refuser(f"couleurs : « {ou} » écrit « {ecrit} » en dur ({nom}) — l'apparence ne cite une "
                        f"couleur que par sa clé.")

# Les pinceaux, pour lire la couleur d'un style, d'un trait ou d'un fond d'icône ; et les
# transparences, que la maquette écrit en « rgba » et l'apparence en opacité.
pinceaux, transparences = {}, {}
for e in apparence.iter(A + "SolidColorBrush"):
    cle = e.get(X + "Key")
    m = re.fullmatch(r"\{StaticResource (\w+)\}", e.get("Color", ""))
    if not m or m.group(1) not in declarees:
        refuser(f"couleurs : le pinceau « {cle} » ne cite pas une couleur déclarée.")
    elif e.get("Opacity") is None:
        pinceaux[cle] = declarees[m.group(1)]
    else:
        transparences[cle] = (declarees[m.group(1)], float(e.get("Opacity")))
# Les ombres de la maquette, du noir en « rgba », ne sont pas dans sa palette : elles
# reviennent à la planche (I4) et aux vignettes (I5).
RGBA = re.compile(r"rgba\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*([\d.]+)\s*\)")
transparences_maquette = {("#%02X%02X%02X" % tuple(int(g) for g in m.groups()[:3]), float(m.group(4)))
                          for m in RGBA.finditer(maquette)}
transparences_maquette = {(c, a) for c, a in transparences_maquette if c in palette}
for couleur, opacite in sorted(transparences_maquette - set(transparences.values())):
    refuser(f"couleurs : la maquette peint {couleur} à {opacite:g} ; aucun pinceau de l'apparence ne le fait.")
for cle, (couleur, opacite) in sorted(transparences.items()):
    if (couleur, opacite) not in transparences_maquette:
        refuser(f"couleurs : le pinceau « {cle} » peint {couleur} à {opacite:g}, ce que la maquette ne fait pas.")

bilan("a", debut, f"{len(palette)} couleurs de la maquette, chacune déclarée une fois sous sa clé et "
                  f"écrite nulle part ailleurs ; {len(transparences)} transparence(s) à l'opacité de la "
                  f"maquette ; {len(HORS_INTERFACE)} couleurs laissées au document ou hors de la phase")

# ------------------------------------------------------------- b. classes et styles

debut = len(echecs)

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
        # La classe conditionnelle du XAML : « Classes.ib="{Binding …}" ».
        for c in re.findall(r'\bClasses\.([A-Za-z]\w*)\s*="', texte):
            employees.setdefault(c, nom)
    return employees

def controler_styles(selecteurs, textes, exceptees):
    fautes = []
    classes, nus = classes_des_selecteurs(selecteurs)
    for variante in nus:
        fautes.append(f"styles : « {variante} » n'est pas porté par une classe — il rhabillerait la fenêtre d'avant la phase.")
    employees = classes_employees((n, t) for n, t in textes if n not in exceptees)
    for c in sorted(classes & set(employees)):
        fautes.append(f"styles : la classe « {c} » de l'apparence est employée par {employees[c]}, qui n'est pas "
                      f"dans la liste des vues de la phase — une vue d'avant la phase, dont la classe est prise, "
                      f"ou une vue de la phase qui manque à la liste.")
    return classes, fautes

styles = [(" ".join(e.get("Selector").split()), e) for e in apparence.iter(A + "Style")]
classes, fautes = controler_styles([s for s, _ in styles], toutes_les_vues, set(vues_de_la_phase))
for f in fautes:
    refuser(f)

# Les témoins : un sélecteur nu, une classe déjà prise.
_, fautes = controler_styles(["Button", "TextBox.champ"], [("Témoin.axaml", '<Button Classes="champ"/>')], set())
if len(fautes) != 2:
    refuser(f"témoin : le contrôle des styles laisse passer un sélecteur nu ou une classe prise ({fautes}).")

# La feuille de style de la maquette, règle par règle, ses déclarations dans l'ordre.
feuille = re.search(r"<style>(.*?)</style>", maquette, re.S).group(1)
regles = {}
for m in re.finditer(r"([^{}]+)\{([^}]*)\}", feuille):
    regles[" ".join(m.group(1).split())] = [tuple(x.strip() for x in d.split(":", 1))
                                           for d in m.group(2).split(";") if ":" in d]

COTES = ("left", "top", "right", "bottom")   # l'ordre d'une « Thickness » d'Avalonia

def bordure_css(valeur):
    """(épaisseur, couleur) d'une bordure CSS : « 1px solid #CDD2D9 », « 0 »."""
    largeur, couleur = re.match(r"\s*([\d.]+)", valeur), HEX.search(valeur)
    return float(largeur.group(1)) if largeur else 0.0, lire_hex(couleur.group(1))[1] if couleur else None

def cascade(noms):
    """Ce que ces règles de la maquette, dans l'ordre de la cascade, donnent à l'élément qui
    les porte toutes : propriété → valeur, la bordure ramenée à ses quatre côtés."""
    valeurs, bord = {}, {c: (0.0, None) for c in COTES}
    for nom in noms:
        for propriete, valeur in regles[nom]:
            if propriete == "border":
                bord = {c: bordure_css(valeur) for c in COTES}
            elif propriete.startswith("border-") and propriete[7:] in COTES:
                bord[propriete[7:]] = bordure_css(valeur)
            else:
                valeurs[propriete] = valeur
    valeurs["border-width"] = tuple(bord[c][0] for c in COTES)
    teintes = {bord[c][1] for c in COTES if bord[c][0] > 0}
    if teintes:
        valeurs["border-color"] = teintes.pop() if len(teintes) == 1 else "plusieurs couleurs"
    return valeurs

def px(valeur):
    return float(valeur.removesuffix("px"))

def cotes_css(valeur):
    """Les quatre côtés d'un « padding » ou d'un « margin » CSS, dans l'ordre d'Avalonia."""
    n = [px(v) for v in valeur.split()]
    haut, droite, bas, gauche = {1: n * 4, 2: n * 2, 3: n + n[1:2], 4: n}[len(n)]
    return gauche, haut, droite, bas

def cotes_avalonia(valeur):
    """Les quatre côtés d'une « Thickness » : « 0 », « 8,0 » (côtés, haut et bas), « 0,1,0,0 »."""
    n = [float(v) for v in re.split(r"[ ,]+", valeur.strip())]
    return tuple({1: n * 4, 2: n * 2, 4: n}[len(n)])

def couleur_css(valeur):
    return "transparent" if valeur == "transparent" else lire_hex(HEX.search(valeur).group(1))[1]

def couleur_avalonia(valeur):
    if valeur == "Transparent":
        return "transparent"
    m = re.fullmatch(r"\{StaticResource (\w+)\}", valeur)
    if not m:
        return f"« {valeur} », qui ne cite pas un pinceau"
    return pinceaux.get(m.group(1), f"« {valeur} », qui n'est pas un pinceau plein de l'apparence")

GRAISSES = {"400": "Normal", "500": "Medium", "600": "SemiBold", "700": "Bold"}
PROPRIETES = {   # propriété CSS → propriété d'Avalonia, lecture de la maquette, lecture de l'apparence
    "width":         ("Width",           px,                   float),
    "height":        ("Height",          px,                   float),
    "border-radius": ("CornerRadius",    px,                   float),
    "font-size":     ("FontSize",        px,                   float),
    "font-weight":   ("FontWeight",      GRAISSES.get,         str),
    "padding":       ("Padding",         cotes_css,            cotes_avalonia),
    "margin":        ("Margin",          cotes_css,            cotes_avalonia),
    "border-width":  ("BorderThickness", tuple,                cotes_avalonia),
    "border-color":  ("BorderBrush",     lambda v: v,          couleur_avalonia),
    "background":    ("Background",      couleur_css,          couleur_avalonia),
    "color":         ("Foreground",      couleur_css,          couleur_avalonia),
}

# Chaque classe de la maquette et le style qui la porte ici, désigné par une variante de son
# sélecteur. Le survol suit « .ib:hover » ; la maquette n'a pas d'état pressé, et pressé,
# un bouton est survolé ; le bouton actif suit « .ib.on », qui vient après « .ib:hover »
# dans la feuille et l'emporte. Fluent peignant les états sur le présentateur du modèle,
# c'est là que la couleur d'un état se lit.
PRESENTATEUR = " /template/ ContentPresenter#PART_ContentPresenter"
BOUTON = "width height border-radius padding border-width background color"
CHAMP = "height border-radius font-size padding border-width border-color background color"
CORRESPONDANCES = [
    ([".ib"],                         "Button.ib",                                  BOUTON),
    ([".ib"],                         "ToggleButton.ib",                            BOUTON),
    ([".ib", ".ib:hover"],            "Button.ib:pointerover" + PRESENTATEUR,       "background color"),
    ([".ib", ".ib:hover"],            "Button.ib:pressed" + PRESENTATEUR,           "background color"),
    ([".ib", ".ib:hover"],            "ToggleButton.ib:pointerover" + PRESENTATEUR, "background color"),
    ([".ib", ".ib:hover"],            "ToggleButton.ib:pressed" + PRESENTATEUR,     "background color"),
    ([".ib", ".ib.on"],               "ToggleButton.ib:checked",                    "color"),
    ([".ib", ".ib:hover", ".ib.on"],  "ToggleButton.ib:checked" + PRESENTATEUR,     "background color"),
    ([".fld"],                        "TextBox.champ",                              CHAMP),
    ([".fld"],                        "ComboBox.champ",                             CHAMP),
    ([".fld"],                        "NumericUpDown.champ",                        CHAMP),
    ([".lbl"],                        "TextBlock.libelle",                          "font-size color"),
    ([".sep"],                        "Border.separateur",                          "width height margin background"),
    ([".sec"],                        "ToggleButton.titreSection",
     "padding font-size font-weight border-width border-color background color"),
]

def valeur_portee(variante, propriete):
    """Ce que l'apparence donne à cette propriété sous cette variante : son dernier mot."""
    valeur = None
    for selecteur, style in styles:
        if variante in [v.strip() for v in selecteur.split(",")]:
            for s in style.iter(A + "Setter"):
                if s.get("Property") == propriete:
                    valeur = s.get("Value")
    return valeur

verifiees = 0
for noms, variante, proprietes_css in CORRESPONDANCES:
    manquantes = [n for n in noms if n not in regles]
    if manquantes:
        refuser(f"styles : la maquette n'a plus de règle {', '.join(manquantes)}, que porte « {variante} ».")
        continue
    voulu = cascade(noms)
    for p in proprietes_css.split():
        if p not in voulu:
            refuser(f"styles : la maquette ne dit plus « {p} » pour {' '.join(noms)}.")
            continue
        propriete, lire_maquette, lire_apparence = PROPRIETES[p]
        attendu, ecrit = lire_maquette(voulu[p]), valeur_portee(variante, propriete)
        porte = lire_apparence(ecrit) if ecrit is not None else "rien"
        verifiees += 1
        if porte != attendu:
            refuser(f"styles : « {variante} » donne à {propriete} {porte}, là où la maquette "
                    f"({' '.join(noms)} {{{p}}}) dit {attendu}.")

bilan("b", debut, f"{len(styles)} styles, tous portés par une classe ({', '.join(sorted(classes))}) qu'aucune "
                  f"vue hors de la phase n'emploie ; {verifiees} tailles et couleurs de la feuille de la "
                  f"maquette, chacune à sa valeur et sous sa clé")

# ---------------------------------------------------------------------- c. les icônes

debut = len(echecs)

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

TRAIT_HORIZONTAL = r"M\s*([\d.]+)[ ,]+([\d.]+)\s*h\s*([\d.]+)"

def miroir(d):
    """Le reflet par x → 24 − x d'un tracé fait de traits horizontaux « Mx y hL » : chaque
    trait garde son sens, de gauche à droite, et part donc de 24 − x − L. Un tracé d'une
    autre sorte n'a pas de reflet ici (None) : la recette refuse plutôt que de deviner."""
    if re.sub(TRAIT_HORIZONTAL, "", d).strip():
        return None
    return "".join(f"M{nombre(24 - float(x) - float(l))} {nombre(y)}h{nombre(l)}"
                   for x, y, l in re.findall(TRAIT_HORIZONTAL, d))

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
            debut_bouton = maquette.rfind("<button", 0, m.start())
            if debut_bouton >= 0 and re.fullmatch(r'<button[^>]*aria-label="' + re.escape(valeur) + r'"[^>]*>\s*',
                                                  maquette[debut_bouton:m.start()]):
                trouves.append(bloc)
        elif sorte == "suivi" and maquette[m.end():].lstrip().startswith(valeur):
            trouves.append(bloc)
    return trouves

def attendu(bloc):
    """Ce que l'apparence doit porter pour ce svg : sa taille, puis un tracé par élément."""
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
    """Ce que l'apparence porte pour cette icône, dans les mêmes termes."""
    toile = icone.find(A + "Canvas")
    elements = []
    for p in (toile if toile is not None else []):
        classes_du_trace = (p.get("Classes") or "").split()
        def couleur(attribut):
            m = re.fullmatch(r"\{StaticResource (\w+)\}", p.get(attribut) or "")
            return pinceaux.get(m.group(1), "?") if m else None
        trait = "courante" if "iconeTrait" in classes_du_trace and p.get("Stroke") is None else couleur("Stroke")
        fond = "courante" if "iconePlein" in classes_du_trace and p.get("Fill") is None else couleur("Fill")
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
    return icone.get("Width"), icone.get("Height"), icone.get(X + "Shared"), grille, elements

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
                fautes.append(f"{cle}, tracé {rang} : {cle_attribut} « {vp} » dans l'apparence, « {va} » dans la maquette.")
    return fautes

table_icones = []
for ligne in (travail / "icones.txt").read_text(encoding="utf-8").splitlines():
    if ligne.strip():
        cle, poste, localisateur = ligne.split(None, 2)
        table_icones.append((cle, poste, localisateur))

ressources = apparence.find(A + "Styles.Resources")
icones_apparence = {e.get(X + "Key"): e for e in ressources if (e.get(X + "Key") or "").startswith("Icone")}
cles_table = [c for c, _, _ in table_icones]
for cle in sorted(set(icones_apparence) - set(cles_table)):
    refuser(f"icônes : {cle} est dans l'apparence mais pas dans la table de la recette (quel poste l'emploie ?).")
for cle in sorted(set(cles_table) - set(icones_apparence)):
    refuser(f"icônes : {cle} est dans la table de la recette mais pas dans l'apparence.")

attendus = {}
for cle, poste, localisateur in table_icones:
    if localisateur.startswith("miroir:"):
        continue
    trouves = svg(localisateur)
    # Plusieurs svg au même repère — les chevrons des sections — doivent être le même.
    if not trouves or any(t != trouves[0] for t in trouves):
        refuser(f"icônes : {cle} — « {localisateur} » désigne {len(trouves)} svg différents dans la maquette.")
        continue
    attendus[cle] = attendu(trouves[0])
reflets = 0
for cle, poste, localisateur in table_icones:
    if localisateur.startswith("miroir:"):
        source = localisateur.split(":", 1)[1]
        if source not in attendus:
            refuser(f"icônes : {cle} est le miroir de {source}, que la maquette ne dessine pas.")
            continue
        taille, elements = attendus[source]
        reflechis = [dict(e, **{"géométrie": miroir(e["géométrie"])}) for e in elements]
        if any(e["géométrie"] is None for e in reflechis):
            refuser(f"icônes : {cle} — la recette ne sait refléter que des traits horizontaux, et {source} en a d'autres.")
            continue
        attendus[cle] = (taille, reflechis)
        reflets += 1
for cle, (taille, elements) in attendus.items():
    if cle in icones_apparence:
        for f in comparer(cle, taille, elements, icones_apparence[cle]):
            refuser("icônes : " + f)

# Les témoins : une icône dont un seul chiffre a bougé ; un reflet qui n'en est pas un.
if "IconeNouveau" in attendus:
    taille, elements = attendus["IconeNouveau"]
    temoin = ET.fromstring(ET.tostring(icones_apparence["IconeNouveau"]))
    trace = temoin.find(A + "Canvas")[0]
    trace.set("Data", trace.get("Data").replace("14", "15", 1))
    if not comparer("IconeNouveau", taille, elements, temoin):
        refuser("témoin : une icône dont le tracé s'écarte de la maquette passe la comparaison.")
if miroir("M4 6h16M4 10h10") != "M4 6h16M10 10h10" or miroir("M4 6h16M4 10l10 2") is not None:
    refuser("témoin : le miroir des traits horizontaux ne rend pas le reflet attendu.")

bilan("c", debut, f"{len(icones_apparence)} icônes, chacune au tracé, au trait et à la taille de la maquette "
                  f"({len(attendus) - reflets} relevées dans ses svg, {reflets} en miroir calculé)")

# ------------------------------------------------- d. icônes employées, icônes qui servent

debut = len(echecs)

def sans_commentaires(nom, texte):
    """Le texte d'une vue sans ses commentaires : un commentaire qui nomme une icône ne
    l'emploie pas."""
    if nom.endswith(".axaml"):
        return re.sub(r"<!--.*?-->", "", texte, flags=re.S)
    return re.sub(r"/\*.*?\*/|//[^\n]*", "", texte, flags=re.S)

# Un emploi de clé : l'extension de balisage, sa forme longue, la clé passée à une
# recherche de ressource en C# ; et, pour une icône, sa clé en chaîne dans le C#, que le
# préfixe « Icone » ne laisse pas confondre avec un autre texte.
EMPLOI = re.compile(r'\{(?:Static|Dynamic)Resource\s+(?:ResourceKey\s*=\s*)?(\w+)\s*\}'
                    r'|ResourceKey="(\w+)"'
                    r'|(?:FindResource|TryFindResource|TryGetResource|GetResourceObservable)\(\s*"(\w+)"'
                    r'|Resources\[\s*"(\w+)"\s*\]')

def cles_employees(nom, texte):
    """Les clés de ressource qu'une vue emploie vraiment."""
    texte = sans_commentaires(nom, texte)
    cles = {next(g for g in m.groups() if g) for m in EMPLOI.finditer(texte)}
    if nom.endswith(".cs"):
        cles |= set(re.findall(r'"(Icone[A-Z]\w*)"', texte))
    return cles

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

def controler_emplois(table_icones, textes, clos, cles_icones):
    fautes, dormantes = [], []
    employees = {}
    for nom, texte in textes:
        for cle in cles_employees(nom, texte):
            if re.match(r"Icone[A-Z]", cle):
                employees.setdefault(cle, nom)
    for cle, nom in sorted(employees.items()):
        if cle not in cles_icones:
            fautes.append(f"icônes : {nom} emploie {cle}, que l'apparence n'a pas.")
    for cle, poste, _ in table_icones:
        if cle in employees:
            continue
        if poste in clos:
            fautes.append(f"icônes : {cle} ne sert dans aucune vue, alors que {poste}, qui l'emploie, est clos.")
        else:
            dormantes.append(poste)
    return fautes, employees, dormantes

clos = postes_clos()
if "P1" not in clos:
    refuser("plan : le tableau de suivi ne se lit plus (P1 n'y est pas clos).")
fautes, employees, dormantes = controler_emplois(table_icones, toutes_les_vues, clos, set(icones_apparence))
for f in fautes:
    refuser(f)

# Les témoins : une vue qui emploie une icône absente ; une icône d'un poste clos que
# seul un commentaire nomme. Sans eux, une clause qui dort ne se distinguerait pas d'une
# clause morte.
fautes, _, _ = controler_emplois([("IconeTemoin", "P1", "")],
                                 [("Témoin.axaml", '<!-- IconeTemoin viendra avec son poste -->\n'
                                                   '<Button Content="{StaticResource IconeInexistante}"/>')],
                                 clos, set(icones_apparence))
if len(fautes) != 2:
    refuser(f"témoin : le contrôle des emplois laisse passer une icône absente ou inemployée ({fautes}).")

servent = sum(1 for c, _, _ in table_icones if c in employees)
attente = ", ".join(f"{p} ({dormantes.count(p)})" for p in sorted(set(dormantes)))
bilan("d", debut, f"{len(employees)} icône(s) employée(s) par une vue, toutes dans l'apparence ; {servent} "
                  f"servent ; la clause dort pour {len(dormantes)}, dont le poste est ouvert : {attente or 'aucune'}")

# ------------------------------------------------------ e. les vues de la phase

debut = len(echecs)
cles_apparence = {e.get(X + "Key") for e in ressources}
releve = collections.defaultdict(dict)
for nom, couleur, compte in table("releve.txt"):
    releve[nom][couleur] = int(compte)

def controler_vues(textes, liste, releve):
    """Une vue de la liste n'écrit aucune couleur de la maquette en dur. Une autre ne cite
    pas l'apparence, et écrit en dur exactement les couleurs de la maquette que dit le
    relevé : une de plus, c'est une vue de la phase qui ne le dit pas."""
    fautes = []
    for nom, texte in textes:
        trouvees = [(n, ecrit, c) for n, ecrit, alpha, c in couleurs_en_dur(texte) if de_la_palette(alpha, c)]
        if nom in liste:
            for n, ecrit, couleur in trouvees:
                fautes.append(f"vues : {nom}:{n} écrit {ecrit}, une couleur de la maquette ({couleur}) — "
                              f"l'apparence la nomme.")
            continue
        citees = sorted(cles_employees(nom, texte) & cles_apparence)
        if citees:
            fautes.append(f"vues : {nom} cite l'apparence ({', '.join(citees)}) sans être dans la liste des "
                          f"vues de la phase.")
        compte = collections.Counter(c for _, _, c in trouvees)
        for couleur in sorted(set(compte) | set(releve.get(nom, {}))):
            prevu = releve.get(nom, {}).get(couleur, 0)
            if compte[couleur] != prevu:
                lieux = ", ".join(f"{n} ({ecrit})" for n, ecrit, c in trouvees if c == couleur) or "aucune"
                fautes.append(f"vues : {nom} écrit {couleur} en dur {compte[couleur]} fois (lignes : {lieux}), "
                              f"le relevé d'avant la phase {prevu} — une vue de la phase entre dans la liste "
                              f"et cite l'apparence ; une vue d'avant la phase qui prend ou perd une couleur "
                              f"le dit au relevé.")
    return fautes

noms_des_vues = {nom for nom, _ in toutes_les_vues}
for nom in vues_de_la_phase:
    if nom not in noms_des_vues:
        refuser(f"vues : {nom}, dans la liste des vues de la phase, n'existe pas.")
for nom in sorted(releve):
    if nom in vues_de_la_phase:
        refuser(f"vues : {nom} est une vue de la phase ; sa ligne au relevé d'avant la phase n'a plus lieu d'être.")
    elif nom not in noms_des_vues:
        refuser(f"vues : {nom}, au relevé, n'existe pas.")
for f in controler_vues(toutes_les_vues, set(vues_de_la_phase), releve):
    refuser(f)

# Les témoins. Une vue de la liste qui écrit en dur l'accent, un filet, le blanc en abrégé,
# par son nom, en pinceau et en composantes : six fautes — et ni l'ombre noire, ni le noir,
# ni le transparent, qui ne sont pas de la palette. Une vue hors de la liste qui cite un
# filet de l'apparence. Une autre qui prend en chiffres le gris de la planche : refusée
# sans relevé, admise avec.
TEMOIN = ('<Border Background="#ff2557c7" BorderBrush="#E2E5EA" BoxShadow="0 1 3 0 #14000000">\n'
          '    <TextBlock Foreground="#FFF" Background="White"/>\n'
          '    <Rectangle Fill="Transparent" Stroke="Black"/>\n'
          '</Border>\n'
          'var fond = Brushes.White; var bord = Color.FromRgb(0x25, 0x57, 0xC7);\n')
fautes = controler_vues([("Témoin.axaml", TEMOIN)], {"Témoin.axaml"}, {})
if len(fautes) != 6:
    refuser(f"témoin : une vue de la phase qui écrit six couleurs de la maquette en dur n'est pas refusée "
            f"six fois ({fautes}).")
if len(controler_vues([("Témoin.axaml", '<Border BorderBrush="{StaticResource Filet}"/>')], set(), {})) != 1:
    refuser("témoin : une vue qui cite l'apparence sans être dans la liste des vues de la phase passe.")
planche = [("Témoin.axaml", '<Border Background="#DCDFE4"/>')]
if len(controler_vues(planche, set(), {})) != 1 or controler_vues(planche, set(), {"Témoin.axaml": {"#DCDFE4": 1}}):
    refuser("témoin : une vue hors de la liste qui écrit en chiffres une couleur de la maquette n'est pas "
            "jugée sur le relevé.")

ecrites = sum(sum(c.values()) for c in releve.values())
bilan("e", debut, f"{len(vues_de_la_phase)} vue(s) de la phase, aucune n'écrit une couleur de la maquette en "
                  f"dur ; hors de la liste, aucune vue ne cite l'apparence, et les couleurs de la maquette "
                  f"écrites en dur sont celles du relevé ({ecrites} dans {len(releve)} fichiers) ; témoins "
                  f"fautifs refusés")

if echecs:
    for message in echecs:
        print(f"    ÉCHEC : {message}", file=sys.stderr)
    sys.exit(1)
PY
echo "  1. les sources : couleurs, styles, icônes, emplois et vues de la phase"

# ------------------------------------------------------------------------ 2. les tests

attendus=6
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
echo "  2. la fenêtre : bouton-icône de 30 × 30 à rayon 6, plein d'accent, survolé et grisé comme la maquette ; icônes, champs, thème intact, vues d'avant la phase inchangées ($joues tests)"

echo "I1 : l'apparence porte la palette, les classes et les icônes de la maquette, sans rien changer aux vues d'avant la phase."
