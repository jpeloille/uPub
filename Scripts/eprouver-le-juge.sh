#!/usr/bin/env bash
#
# Éprouve le filet en le trouant : sabote le code, une chose à la fois, et vérifie
# que quelque chose s'en aperçoit.
#
# C'est la seule épreuve qui compte vraiment. Un contrôle qui passe ne prouve rien
# tant qu'on ne l'a pas vu échouer — le poste P2 l'avait fait à la main en son temps
# (« le contrôle a été éprouvé en le faisant échouer »), sans rien laisser derrière.
# Ceci le laisse.
#
# Chaque sabotage remplace un bout de code par un autre, joue la vérification, et
# attend un échec. Un sabotage NON détecté est un trou dans le filet : le script le
# nomme et sort en erreur.
#
#   Scripts/eprouver-le-juge.sh            # tous les sabotages
#   Scripts/eprouver-le-juge.sh 'motif'    # ceux dont l'intitulé répond au motif (grep -E)
#
# L'arbre de travail doit être propre : le script écrit dans les sources et les
# restaure par « git checkout ». Il refuse de tourner autrement, pour ne pas
# emporter un travail en cours.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$racine"

if [ -n "$(git status --porcelain)" ]; then
    echo "ÉCHEC : l'arbre de travail n'est pas propre." >&2
    echo "Ce script sabote les sources et les restaure par « git checkout » :" >&2
    echo "il emporterait le travail en cours." >&2
    exit 2
fi

# Verrou. Ce script écrit dans les sources et les restaure par « git checkout » :
# quiconque travaillerait dans l'arbre pendant qu'il tourne verrait son travail
# emporté sans un mot. C'est arrivé une fois, le 6 septembre 2026, à deux fichiers
# de documentation — et rien ne l'avait signalé.
verrou="$racine/.juge-en-cours"
if [ -e "$verrou" ]; then
    echo "ÉCHEC : une épreuve du juge est déjà en cours ($verrou)." >&2
    echo "Ce script restaure l'arbre en sortant : deux instances se marcheraient dessus." >&2
    exit 2
fi
touch "$verrou"

restaurer() { git checkout -- . 2>/dev/null || true; }
nettoyer() { restaurer; rm -f "$verrou"; }
trap nettoyer EXIT

trous=0
sabotages=0
filtre="${1:-}"

# $1 intitulé · $2 fichier · $3 texte d'origine · $4 texte saboté
# $5, facultatif : le garde-fou attendu (motif grep -E). Un sabotage attrapé par un
# autre que lui est compté comme un trou : la recette d'un poste qui n'attrape pas son
# propre sabotage ne prouve rien, même si un test voisin le voit à sa place.
saboter() {
    local intitule="$1" fichier="$2" avant="$3" apres="$4" attendu="${5:-}"
    if [ -n "$filtre" ] && ! grep -qE "$filtre" <<< "$intitule"; then return; fi
    sabotages=$((sabotages + 1))

    if ! grep -qF "$avant" "$fichier"; then
        echo "  ?  $intitule — le code visé a changé, sabotage impossible" >&2
        echo "     ($fichier ne contient plus « ${avant:0:60}… »)" >&2
        trous=$((trous + 1))
        return
    fi

    python3 - "$fichier" "$avant" "$apres" <<'PY'
import sys, pathlib
f, avant, apres = sys.argv[1], sys.argv[2], sys.argv[3]
p = pathlib.Path(f)
p.write_text(p.read_text().replace(avant, apres, 1))
PY

    local sortie=0
    Scripts/verifier.sh > /tmp/juge-$$.txt 2>&1 || sortie=$?
    if [ "$sortie" = 0 ]; then
        echo "  ✗  $intitule — NON DÉTECTÉ" >&2
        trous=$((trous + 1))
    elif [ "$sortie" = 3 ]; then
        # verifier.sh sort en 3 quand le compilateur a planté deux fois : rien n'a
        # été jugé, ce n'est pas une prise.
        echo "  ?  $intitule — le compilateur a planté, rien n'a été jugé" >&2
        trous=$((trous + 1))
    else
        # Qui l'a attrapé, précisément : tous les tests et toutes les recettes en
        # échec, pas le premier venu. Sans cette précision, on croirait le filet plus
        # large qu'il n'est — trois sabotages « attrapés par le contrôle » peuvent
        # n'être qu'un seul et même garde-fou. Les recettes portent le code de leur
        # poste en majuscule (« C4-champs »).
        local tests recettes autres coupable
        tests="$(grep -oE "[A-Za-z0-9_]+Tests\.[A-Za-z0-9_]+" /tmp/juge-$$.txt | sort -u | head -4 | paste -sd' ' || true)"
        recettes="$(grep -oE '^  [A-Za-z0-9-]+ : ÉCHOUE' /tmp/juge-$$.txt | sed 's/^  //; s/ : ÉCHOUE//' | paste -sd' ' || true)"
        autres="$(grep -oE '^ÉCHEC : .*' /tmp/juge-$$.txt | grep -v 'les tests ne passent pas' | head -1 || true)"
        coupable="$(printf '%s\n' "$tests" "$recettes" "$autres" | sed '/^$/d' | paste -sd' ' )"
        [ -z "$coupable" ] && coupable="$(grep -oE '^ÉCHEC : .*' /tmp/juge-$$.txt | head -1 || true)"
        [ -z "$coupable" ] && coupable="(inconnu : voir la sortie de verifier.sh)"
        if [ -n "$attendu" ] && ! grep -qE "$attendu" <<< "$coupable"; then
            echo "  ✗  $intitule — attrapé, mais pas par « $attendu »" >&2
            echo "       attrapé par : $coupable" >&2
            trous=$((trous + 1))
        else
            echo "  ✓  $intitule"
            echo "       attrapé par : $coupable"
        fi
    fi

    rm -f /tmp/juge-$$.txt
    restaurer
}

echo "Épreuve du juge — chaque ligne sabote une chose et attend un échec."
echo

saboter "l'aimantation ne s'accroche plus" \
    MyPublisher.Core/Models/Snap.cs \
    'var bestDistance = tolerance;' \
    'var bestDistance = 0.0;'

saboter "une copie de bloc perd son interligne" \
    MyPublisher.Core/Models/Frame.cs \
    'LineHeight = LineHeight,' \
    ''

saboter "la migration de la version 1 ne fait plus rien" \
    MyPublisher.Core/Services/FormatMigrations.cs \
    'if (version < 2) ToVersion2(root);' \
    ''

saboter "le JSON n'est plus écrit en clair" \
    MyPublisher.Core/Services/DocumentSerializer.cs \
    'Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping' \
    'Encoder = null'

saboter "un groupe ne met plus ses blocs à l'échelle" \
    MyPublisher.Core/Models/Frame.cs \
    'var factor = newSize / oldSize;' \
    'var factor = 1.0;'

saboter "la justification ne repartit plus le blanc" \
    MyPublisher.Core/Models/Compositeur.cs \
    "var part = manque / blancs;" \
    "var part = 0.0;"

saboter "l interligne redevient un multiple du corps" \
    MyPublisher.Core/Models/Compositeur.cs \
    "naturelle * (multiple > 0 ? multiple : 1);" \
    "multiple;"

# Q12 n'a pas de sabotage ici : sa recette, « verifier-dependances.sh », demande le
# réseau et se lance à part. Sa contre-épreuve — un projet témoin qui tire la version
# fautive doit y être signalé — la fait échouer à chaque passage.

# Les recettes de C1, C2 et C3, écrites au poste P3 : un sabotage chacune, sur ce que
# son poste avait promis et que rien d'autre ne gardait.

# C1 promettait un filet qui passe SUR l'image, à l'écran comme au PDF. Rangé sous elle,
# il se tait partout ailleurs : l'image le recouvre exactement là où personne ne
# regarde, sur la moitié intérieure du bord. Le sabotage vise la PREMIÈRE occurrence du
# contour dans PageCanvas.axaml, qui est celle du gabarit d'image.
saboter "à l'écran, le filet d'une image passe sous elle" \
    MyPublisher/Views/PageCanvas.axaml \
    '<Rectangle Classes="frameStroke"/>' \
    '<Rectangle Classes="frameStroke" ZIndex="-1"/>' \
    'C1-fond-et-filet'

# C2b : un repère libre qui sort de la table d'aimantation ne casse aucun calcul — il
# cesse seulement de servir. Seul le geste le voit.
saboter "les repères libres n'aimantent plus les blocs" \
    MyPublisher.Core/Models/Document.cs \
    '.. VerticalRulerGuides.Select(guide => guide.Position)' \
    '' \
    'C2-reperes'

# C3b : « aligner à gauche » qui aligne sur le bord droit donne encore aux blocs un
# bord commun — mais pas celui du plus à gauche.
saboter "aligner à gauche aligne sur le bord droit" \
    MyPublisher/ViewModels/MainWindowViewModel.cs \
    'AlignEach((f, r) => (r.Left - f.Left, 0), "Alignés à gauche")' \
    'AlignEach((f, r) => (r.Right - f.Right, 0), "Alignés à gauche")' \
    'C3-selection-groupe'

# Trois trous que la relecture des recettes de C1, C2 et C3 a trouvés, et que rien
# n'attrapait avant elle.

# C1 : le seul cadrage que l'export découpe est « UniformToFill ». Si la découpe
# survit, le filet tracé ensuite est rogné au bloc, de sa moitié extérieure : 1 pt au
# lieu de 2. Le texte visé court sur deux lignes, le commentaire qui la garde et la
# restauration elle-même — celle-ci seule n'est pas unique dans le fichier.
saboter "au PDF, la découpe d'une image rogne son filet" \
    MyPublisher.Core/Services/PdfExporter.cs \
    $'// ensuite serait rogné au bloc, à la moitié de son épaisseur.\n                gfx.Restore(state);' \
    $'// ensuite serait rogné au bloc, à la moitié de son épaisseur.' \
    'C1-fond-et-filet'

# C2b : la règle de gauche, qui pose les repères horizontaux, est une branche à part
# du geste. Débranchée, la règle du haut marche toujours.
saboter "la règle de gauche ne pose plus de repère" \
    MyPublisher/Views/PageCanvas.axaml.cs \
    'LeftRuler.PointerPressed += OnRulerPressed;' \
    '// LeftRuler.PointerPressed += OnRulerPressed;' \
    'C2-reperes'

# C3b : un alignement qui n'ouvre pas sa transaction aligne très bien — il ne
# s'annule plus, et c'est le groupement suivant qui emporte son état.
saboter "aligner ne s'annule plus" \
    MyPublisher/ViewModels/MainWindowViewModel.cs \
    $'        BeginEdit();\n        foreach (var frame in Selection)\n        {\n            var (dx, dy) = shift(frame.Extent, reference);' \
    $'        foreach (var frame in Selection)\n        {\n            var (dx, dy) = shift(frame.Extent, reference);' \
    'C3-selection-groupe'

# Les quatre suivants éprouvent les recettes de B1, C4, C6 et R4, écrites le
# 11 octobre 2026 pour des postes clos sans recette rejouable. Chacun casse ce que SA
# recette mesure. Joués un à un contre les tests, le contrôle et chaque recette avant
# d'être écrits ici : ceux de C4 et de C6 passent tout le reste et ne tombent que sur
# leur recette ; celui de R4 passe les 311 tests et tombe sur les recettes de R2, R3
# et R4 ; celui de B1 tombe sur les tests « poste=B1 », que sa recette appelle.

# Le défaut que le poste B1 existe pour empêcher : le modèle s'ouvre comme un document,
# et « Enregistrer » l'écrase.
saboter "un document parti d un modèle en garde le chemin" \
    MyPublisher/ViewModels/MainWindowViewModel.cs \
    'Adopt(document, null, ' \
    'Adopt(document, path, ' \
    'B1-modeles'

# Le folio de l'écran resterait juste : seul le PDF, relu page par page, le voit.
saboter "le folio du PDF reste à 1 sur toutes les pages" \
    MyPublisher.Core/Services/PdfExporter.cs \
    'new FieldContext(index + 1, document.Pages.Count' \
    'new FieldContext(1, document.Pages.Count' \
    'C4-champs'

# Une marge unique appliquée des deux côtés : le fer gauche reste juste, et seul un
# texte calé à droite trahit la confusion.
saboter "l export retire la marge gauche à la place de la droite" \
    MyPublisher.Core/Services/PdfExporter.cs \
    'Math.Max(1, text.Width - text.PaddingLeft - text.PaddingRight),' \
    'Math.Max(1, text.Width - text.PaddingLeft - text.PaddingLeft),' \
    'C6-marges'

# Le défaut d'origine de R4, que le banc avait trouvé le 6 septembre 2026 : l'écran
# avançait de 12,00 pt quand le PDF avançait de 13,93. Aucun test ne le voit.
saboter "l écran reprend le corps pour hauteur naturelle" \
    MyPublisher/Converters/InterligneConverter.cs \
    'return (-metriques.Ascent + metriques.Descent + metriques.LineGap) * corps / em;' \
    'return corps;' \
    'R4-interligne'

# Les deux suivants viennent de la relecture des recettes de C4 et de C6 : chacun
# passait les tests, le contrôle et les dix-sept recettes d'alors. Les deux recettes
# ont été reprises pour les voir, et chaque sabotage joué à la main contre la sienne
# avant d'être écrit ici.

# Le jeu de coordonnées que nomme « ActiveSetName » est ignoré : on imprime le dernier.
# La recette de C4 exporte la carte trois fois, chaque jeu actif à son tour.
saboter "on prend le dernier jeu de coordonnées au lieu de l actif" \
    MyPublisher.Core/Services/BusinessInfoStore.cs \
    'Sets.FirstOrDefault(set => set.SetName == ActiveSetName) ?? Sets.FirstOrDefault() ?? new BusinessInfo();' \
    'Sets.LastOrDefault() ?? new BusinessInfo();' \
    'C4-champs'

# La marge du haut oubliée à l'écran, le PDF restant juste : seul le bloc « 20 pt en
# haut » de la recette de C6 le voit.
saboter "l écran ignore la marge du haut" \
    MyPublisher/Views/TexteCompose.cs \
    'var ligneDeBase = bloc.PaddingTop + descente' \
    'var ligneDeBase = descente' \
    'C6-marges'

# Q2 : le remède lui-même. Le geste réel — changer de page — ne l'éprouve pas, le
# conteneur retirant le bloc de lui-même ; c'est le contrôle qui quitte l'arbre en
# gardant son bloc qui doit tomber.
saboter "le texte composé ne se détache plus du bloc en quittant l'arbre" \
    MyPublisher/Views/ControleDeBloc.cs \
    'if (Bloc is { } bloc) Desabonner(bloc);' \
    '_ = Bloc;' \
    'Q2-Q3-fuite-et-cache'

saboter "rouvrir un document ne fait plus oublier les images manquantes" \
    MyPublisher/ViewModels/MainWindowViewModel.cs \
    'BitmapCache.OublierLesEchecs();' \
    '' \
    'Q2-Q3-fuite-et-cache'

# Q3, l'autre moitié : seulement les échecs. Vider tout le cache réparerait le défaut,
# et c'est pourquoi rien d'autre que l'épingle du test ne le verrait.
saboter "rouvrir un document vide tout le cache d'images, réussites comprises" \
    MyPublisher/Converters/Conv.cs \
    'foreach (var cle in echecs) Cache.Remove(cle);' \
    'Cache.Clear();' \
    'Q2-Q3-fuite-et-cache'

# Q2, l'autre chemin du désabonnement : un contrôle affiché qui change de bloc sans
# quitter l'arbre. Aucun des trois premiers tests ne le voit.
saboter "le texte composé reste accroché à l'ancien bloc quand on lui en donne un autre" \
    MyPublisher/Views/ControleDeBloc.cs \
    'if (e.OldValue is TextFrame ancien) Desabonner(ancien);' \
    '_ = e.OldValue;' \
    'Q2-Q3-fuite-et-cache'

# Q3 : les trois gestes qui passent par l'ouverture, et deux qui n'y passent pas.
saboter "partir d'un modèle ne fait plus oublier les images manquantes" \
    MyPublisher/ViewModels/MainWindowViewModel.cs \
    'BitmapCache.OublierLesEchecs();' \
    'if (path is not null) BitmapCache.OublierLesEchecs();' \
    'Q2-Q3-fuite-et-cache'

saboter "annuler un geste fait relire les images manquantes, comme une réouverture" \
    MyPublisher/ViewModels/MainWindowViewModel.cs \
    'newValue.PropertyChanged += OnDocumentPropertyChanged;' \
    'newValue.PropertyChanged += OnDocumentPropertyChanged; BitmapCache.OublierLesEchecs();' \
    'Q2-Q3-fuite-et-cache'

saboter "une image remise en place puis réinsérée reste vide" \
    MyPublisher/ViewModels/MainWindowViewModel.cs \
    'BitmapCache.Invalidate(path);' \
    '_ = path;' \
    'Q2-Q3-fuite-et-cache'

saboter "une image remise en place puis choisie pour un fond reste vide" \
    MyPublisher/ViewModels/MainWindowViewModel.cs \
    'BitmapCache.Invalidate(chemin);' \
    '_ = chemin;' \
    'Q2-Q3-fuite-et-cache'

# Le poste Q1. Neuf sabotages et non un : le calcul peut être juste et le
# branchement faux, et chacun des deux se casse à sa façon. La recette de PLAN.md
# demande que neutraliser le décalage fasse tomber le test pur, le test d'interface et
# la recette ; le premier sabotage le fait, les trois suivants trouent le branchement,
# deux autres l'avis qui accorde les zones entre elles, et les trois derniers chacune
# des règles de style.
saboter "taper devant un fragment ne le décale plus" \
    MyPublisher.Core/Models/Recalage.cs \
    '(debut, fin) = (debut + edition.Inseres, fin + edition.Inseres);' \
    '(debut, fin) = (debut, fin);' \
    'Q1-styles-frappe|RecalageTests|FrappeTests'

saboter "un saut de ligne tapé ne décale plus les écarts de paragraphe" \
    MyPublisher.Core/Models/Recalage.cs \
    'Inseres: Sauts(apres, point, point + edition.Inseres),' \
    'Inseres: 0,' \
    'Q1-styles-frappe|RecalageTests|FrappeTests'

saboter "la saisie sur la page ne passe plus par le recalage" \
    MyPublisher/Views/PageCanvas.axaml \
    'Text="{Binding Saisie, Mode=TwoWay}"' \
    'Text="{Binding Text, Mode=TwoWay}"' \
    'Q1-styles-frappe|RecalageTests|FrappeTests'

saboter "une case de tableau ne recale plus ses fragments à la saisie" \
    MyPublisher.Core/Models/Table.cs \
    'Recalage.Appliquer(Text, nouveau, Spans, null);' \
    '' \
    'Q1-styles-frappe|RecalageTests|FrappeTests'

# Les zones se lient à « Saisie » et la saisie écrit « Text » : sans l'avis que l'un
# change avec l'autre, le panneau garde un texte périmé et la frappe suivante y
# efface ce qu'on a tapé sur la page.
saboter "le panneau n'apprend plus qu'on a tapé sur la page" \
    MyPublisher.Core/Models/Frame.cs \
    '[NotifyPropertyChangedFor(nameof(Saisie))]' \
    '' \
    'Q1-styles-frappe|RecalageTests|FrappeTests'

saboter "le champ d'une case n'apprend plus que son texte a changé" \
    MyPublisher.Core/Models/Table.cs \
    '[NotifyPropertyChangedFor(nameof(Saisie))]' \
    '' \
    'Q1-styles-frappe|RecalageTests|FrappeTests'

# Les trois règles de Publisher et de Word, chacune à sa ligne.
saboter "en tête de paragraphe, la frappe ne prend plus le style qui suit" \
    MyPublisher.Core/Models/Recalage.cs \
    "if (enTete && point < avant.Length && avant[point] is not ('\\n' or '\\r')) return point;" \
    '' \
    'Q1-styles-frappe|RecalageTests|FrappeTests'

saboter "un remplacement ne prend plus le style du premier remplacé" \
    MyPublisher.Core/Models/Recalage.cs \
    'if (edition.Supprimes > 0) return point;' \
    '' \
    'Q1-styles-frappe|RecalageTests|FrappeTests'

saboter "un collage qui porte du texte est poussé à gauche comme un saut" \
    MyPublisher.Core/Models/Recalage.cs \
    "if (apres.AsSpan(edition.Debut, edition.Inseres).ContainsAnyExcept('\\n', '\\r')) return edition;" \
    '' \
    'Q1-styles-frappe|RecalageTests|FrappeTests'

# Salve 2, lot « rendu » : Q4, Q5, Q6 et Q16, les écarts entre l'écran et le PDF.
# Chacun éprouvé à la main avant d'être écrit ici : appliqué, la cible tombée, restauré.

# Q4 : l'écran rentrait le filet d'une demi-épaisseur. La marge qui le recentre ôtée,
# le contour commun des blocs rentre de nouveau d'une demi-épaisseur — la recette de Q4
# le voit sur douze côtés sur vingt-quatre (image, texte, groupe), celle de C1,
# resserrée, sur ses douze.
saboter "à l'écran, le filet d'un bloc rentre de nouveau dans le bloc" \
    MyPublisher/Views/PageCanvas.axaml \
    $'            <Setter Property="Margin" Value="{Binding StrokeThickness, Converter={x:Static conv:Conv.FiletCentre}}"/>\n' \
    '' \
    'Q4-filet|C1-fond'

# L'ellipse a son propre élément : seule la recette de Q4 la porte.
saboter "à l'écran, le filet d'une ellipse rentre dans la forme" \
    MyPublisher/Views/PageCanvas.axaml \
    $'<Ellipse IsVisible="{Binding IsEllipse}"\n                         Margin="{Binding StrokeThickness, Converter={x:Static conv:Conv.FiletCentre}}"' \
    '<Ellipse IsVisible="{Binding IsEllipse}"' \
    'Q4-filet'

# Q5 : l'export ne découpe plus les blocs d'un groupe. Le texte de 400 pt court de
# nouveau 280 pt au-delà de son groupe dans le PDF, et pas à l'écran.
saboter "l'export ne découpe plus au groupe" \
    MyPublisher.Core/Services/PdfExporter.cs \
    $'            gfx.IntersectClip(new XRect(zone.Left, zone.Top, zone.Width, zone.Height));\n' \
    '' \
    'Q5-groupe'

# Q5 : la découpe ramenée au cadre exact du porteur rogne de moitié, à l'écran comme
# au PDF, le filet d'un bloc posé sur le bord d'un groupe ou d'un tableau.
saboter "la découpe d'un groupe rogne le filet de ses blocs" \
    MyPublisher.Core/Models/Decoupe.cs \
    'return new Extent(-marge, -marge, largeur + marge, hauteur + marge);' \
    'return new Extent(0, 0, largeur, hauteur);' \
    'Q5-groupe|DecoupeTests'

# Q6 : la découpe d'un remplissage par image oublie l'arrondi, au PDF puis à l'écran.
# Avant Q6, ces deux-là traversaient toute la vérification : la page de C5 ne portait
# pas de bloc arrondi rempli d'une image.
saboter "au PDF, un remplissage par image oublie l'arrondi du bloc" \
    MyPublisher.Core/Services/PdfExporter.cs \
    $'        else if (pourtour.Arrondi > 0)\n        {\n            chemin.AddRoundedRectangle(' \
    $'        else if (pourtour.Arrondi < 0)\n        {\n            chemin.AddRoundedRectangle(' \
    'C5-fonds'

saboter "à l'écran, un remplissage par image oublie l'arrondi du bloc" \
    MyPublisher/Views/FondBloc.cs \
    ': (Geometry)new RectangleGeometry(rect, Arrondi, Arrondi);' \
    ': (Geometry)new RectangleGeometry(rect);' \
    'C5-fonds'

# Q16 : le pointillé d'un bloc image ne regarde plus si l'image est chargée, puis ne
# regarde plus l'interrupteur des repères. Chacune des deux conditions a son test.
saboter "le pointillé d'un bloc image se dessine sur l'image chargée" \
    MyPublisher/Views/PageCanvas.axaml \
    $'                            <Binding Path="#Photo.Source" Converter="{x:Static ObjectConverters.IsNull}"/>\n' \
    '' \
    'Q16-pointille|PointilleTests'

saboter "le pointillé d'un bloc image n'obéit plus aux repères" \
    MyPublisher/Views/PageCanvas.axaml \
    $'                            <Binding Path="#Board.((vm:MainWindowViewModel)DataContext).ShowGuides"/>\n                            <Binding Path="#Photo.Source"' \
    '                            <Binding Path="#Photo.Source"' \
    'Q16-pointille|PointilleTests'

echo
if [ "$trous" = 0 ]; then
    echo "Le juge sait échouer : $sabotages sabotages, $sabotages attrapés."
else
    echo "TROUS DANS LE FILET : $trous sabotage(s) sur $sabotages non détecté(s)." >&2
    echo "Un sabotage non détecté est une régression que la boucle laisserait passer." >&2
    exit 1
fi
