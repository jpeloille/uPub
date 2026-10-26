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
#   Scripts/eprouver-le-juge.sh 'motif'    # ceux dont l'intitulé, ou le garde-fou attendu,
#                                          # répond au motif (grep -E) : le garde-fou porte
#                                          # le code du poste (« I7-souligne »), si bien que
#                                          # « I7 » retient les sabotages du poste I7, et
#                                          # « I2|B7 » ceux que la recette I2-B7 doit attraper.
#                                          # Un motif qui ne retient rien est un échec, pas un succès
#   Scripts/eprouver-le-juge.sh --cibles   # sans rien saboter : chaque texte visé est-il
#                                          # présent, et une seule fois ? (quelques secondes)
#   Scripts/eprouver-le-juge.sh --tranche 2/4 [motif]
#                                          # un sabotage sur quatre, à partir du deuxième :
#                                          # quatre copies de travail font le tout en parallèle
#
# L'arbre de travail doit être propre : le script écrit dans les sources et les
# restaure par « git checkout ». Il refuse de tourner autrement, pour ne pas
# emporter un travail en cours.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$racine"

# --cibles ne lit que les sources : ni arbre propre exigé, ni verrou, ni restauration.
cibles=0
if [ "${1:-}" = "--cibles" ]; then
    cibles=1
    shift
fi
# --tranche k/n : seuls les sabotages de rang k, k+n, k+2n… (rangs comptés à partir de 1,
# après le filtre). Plus d'une centaine de sabotages à quatre minutes chacun ne tiennent
# pas dans une soirée ; quatre copies de travail les jouent en parallèle.
tranche=1
tranches=1
if [ "${1:-}" = "--tranche" ]; then
    if ! [[ "${2:-}" =~ ^([0-9]+)/([0-9]+)$ ]] || [ "${BASH_REMATCH[1]}" -lt 1 ] ||
       [ "${BASH_REMATCH[1]}" -gt "${BASH_REMATCH[2]}" ]; then
        echo "ÉCHEC : --tranche attend k/n, avec 1 ≤ k ≤ n (reçu « ${2:-} »)." >&2
        exit 2
    fi
    tranche="${BASH_REMATCH[1]}"
    tranches="${BASH_REMATCH[2]}"
    shift 2
fi
rang=0

if [ "$cibles" = 0 ] && [ -n "$(git status --porcelain)" ]; then
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
if [ "$cibles" = 1 ]; then
    :
elif [ -e "$verrou" ]; then
    echo "ÉCHEC : une épreuve du juge est déjà en cours ($verrou)." >&2
    echo "Ce script restaure l'arbre en sortant : deux instances se marcheraient dessus." >&2
    exit 2
fi
if [ "$cibles" = 0 ]; then
    touch "$verrou"
    restaurer() { git checkout -- . 2>/dev/null || true; }
    nettoyer() { restaurer; rm -f "$verrou"; }
    trap nettoyer EXIT
fi

trous=0
sabotages=0
filtre="${1:-}"

# $1 intitulé · $2 fichier · $3 texte d'origine · $4 texte saboté
# $5, facultatif : le garde-fou attendu (motif grep -E). Un sabotage attrapé par un
# autre que lui est compté comme un trou : la recette d'un poste qui n'attrape pas son
# propre sabotage ne prouve rien, même si un test voisin le voit à sa place.
saboter() {
    local intitule="$1" fichier="$2" avant="$3" apres="$4" attendu="${5:-}"
    # Le motif lit l'intitulé et le garde-fou attendu : les intitulés ne portent pas le code
    # de leur poste, la recette qui doit les attraper, si.
    if [ -n "$filtre" ] && ! grep -qE "$filtre" <<< "$intitule" && ! grep -qE "$filtre" <<< "$attendu"; then return; fi
    rang=$((rang + 1))
    if [ $(( (rang - 1) % tranches )) != $((tranche - 1)) ]; then return; fi
    sabotages=$((sabotages + 1))

    # Le texte visé doit y être une fois et une seule : présent plusieurs fois, le
    # sabotage frapperait la première occurrence, qui n'est peut-être pas celle que
    # l'intitulé décrit.
    local occurrences
    occurrences="$(python3 -c 'import sys, pathlib; print(pathlib.Path(sys.argv[1]).read_text().count(sys.argv[2]))' "$fichier" "$avant")"
    if [ "$occurrences" = 0 ]; then
        echo "  ?  $intitule — le code visé a changé, sabotage impossible" >&2
        echo "     ($fichier ne contient plus « ${avant:0:60}… »)" >&2
        trous=$((trous + 1))
        return
    elif [ "$occurrences" != 1 ]; then
        echo "  ?  $intitule — le texte visé est ambigu, sabotage refusé" >&2
        echo "     ($fichier contient $occurrences fois « ${avant:0:60}… »)" >&2
        trous=$((trous + 1))
        return
    fi
    if [ "$cibles" = 1 ]; then return; fi

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
    'LineHeight = LineHeight, PaddingLeft = PaddingLeft,' \
    'PaddingLeft = PaddingLeft,'

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

# I6 : naviguer dans la planche, et garder les proportions. Le poste n'avait aucun sabotage
# (règle 11) ; la relecture du 26 octobre 2026 lui en donne sept, chacun sur une clause de la
# recette du plan ou sur le défaut qu'elle a refermé. « eprouver-le-juge.sh I6 » les joue seuls.

# La main suit le pointeur : figée sur son départ, la planche ne défile plus.
saboter "la main (I6) ne fait plus défiler la planche" \
    MyPublisher/Views/PageCanvas.Navigation.cs \
    'Scroller.Offset = _defilementDeDepart - chemin;' \
    'Scroller.Offset = _defilementDeDepart;' \
    'I6-navigation|NavigationTests'

# La loupe zoome encore, mais le point visé s'en va.
saboter "la loupe (I6) ne garde plus le point visé sous le pointeur" \
    MyPublisher/Views/PageCanvas.Navigation.cs \
    'Scroller.Offset = ici - dansLaZone;' \
    '' \
    'I6-navigation|NavigationTests'

# Au passage montant, la zone qui défile consomme la molette : sur une page qui déborde,
# Ctrl + molette fait défiler au lieu de zoomer.
saboter "Ctrl + molette (I6) laissée à la zone qui défile" \
    MyPublisher/Views/PageCanvas.axaml.cs \
    'Scroller.AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);' \
    'Scroller.PointerWheelChanged += OnPointerWheelChanged;' \
    'I6-navigation|NavigationTests'

# « Page entière » se fie à son calcul : l'arrondi de la mise en page la fait dépasser d'un pixel.
saboter "« Page entière » (I6) ne remesure pas la feuille après la mise en page" \
    MyPublisher/Views/PageCanvas.Navigation.cs \
    'if (depasse <= 1) break;' \
    'break;' \
    'I6-navigation|NavigationTests'

# Case cochée, la largeur saisie n'entraîne plus la hauteur.
saboter "la case des proportions (I6) n'entraîne plus l'autre côté à la saisie" \
    MyPublisher/ViewModels/MainWindowViewModel.Navigation.cs \
    'var (nouvelleLargeur, nouvelleHauteur) = AuMemeRapport(bloc.Width, bloc.Height, largeur, hauteur);' \
    'var (nouvelleLargeur, nouvelleHauteur) = (largeur ?? bloc.Width, hauteur ?? bloc.Height);' \
    'I6-navigation|NavigationTests'

# Le coin tiré n'écoute plus que Maj, comme avant le poste : la case n'y fait rien.
saboter "le coin tiré (I6) ignore la case des proportions" \
    MyPublisher/Views/PageCanvas.axaml.cs \
    'else ApplyResize(frame, page, maj != _vm.ConserverLesProportions);' \
    'else ApplyResize(frame, page, maj);' \
    'I6-navigation|NavigationTests'

# Le défaut que la relecture a refermé : une prise de la main faisait la moitié d'un double-clic.
saboter "une prise de la main (I6) fait la moitié d'un double-clic" \
    MyPublisher/Views/PageCanvas.axaml.cs \
    'if (MainArmee || LoupeArmee || _pressionPrecedenteNaviguait) return;' \
    'if (MainArmee || LoupeArmee) return;' \
    'I6-navigation|NavigationTests'

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
    '</Image>
                <Rectangle Classes="frameStroke"/>' \
    '</Image>
                <Rectangle Classes="frameStroke" ZIndex="-1"/>' \
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
    'new FieldContext(index + 1, document.Pages.Count, now' \
    'new FieldContext(1, document.Pages.Count, now' \
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
    'var haut = bloc.PaddingTop + descente + ligne.Haut;' \
    'var haut = descente + ligne.Haut;' \
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

# I7 — le souligné. Vingt-six sabotages : le style effectif, les étendues que la
# composition souligne, la géométrie et le motif des traits, chacun des deux rendus —
# l'exposant et la couleur compris —, le fichier, l'import, la copie et la commande. Les rendus partagent le noyau ; un sabotage du
# noyau tombe des deux côtés à la fois, et c'est la mesure contre la table « post » de la
# recette, jamais la comparaison de l'écran au PDF, qui le voit. Placés après Q1 et non
# en fin de liste, où les autres postes de la phase I ajoutent les leurs en même temps.
saboter "un fragment ne dit plus son souligné au style effectif" \
    MyPublisher.Core/Models/StyleSpan.cs \
    'fragment.Underline ?? styles[i].Underline,' \
    'styles[i].Underline,' \
    'I7-souligne|Souligne(Ecran)?Tests'

saboter "le style commun d'un bloc perd son souligné" \
    MyPublisher.Core/Models/Jeux.cs \
    'bloc.FontFamily, bloc.FontSize, bloc.Bold, bloc.Italic, bloc.Underline, bloc.Foreground, 0));' \
    'bloc.FontFamily, bloc.FontSize, bloc.Bold, bloc.Italic, Souligne.Aucun, bloc.Foreground, 0));' \
    'I7-souligne|Souligne(Ecran)?Tests'

saboter "un souligné continu s'arrête au bout de son morceau, avant le blanc élargi" \
    MyPublisher.Core/Models/Compositeur.cs \
    '? ligne.Morceaux[m + 1].X + decalages[m + 1]' \
    '? debut + morceau.Largeur' \
    'I7-souligne|Souligne(Ecran)?Tests'

saboter "un souligné « mots seuls » passe sous les blancs qui suivent un mot" \
    MyPublisher.Core/Models/Compositeur.cs \
    'while (j < texte.Length && !char.IsWhiteSpace(texte[j])) j++;' \
    'j = texte.Length;' \
    'I7-souligne|Souligne(Ecran)?Tests'

# Repris après relecture : « Mots » ne coupe pas à l'insécable, qui n'était nue qu'au
# bout d'un fragment.
saboter "un souligné « mots seuls » passe sous une insécable au milieu d'un morceau" \
    MyPublisher.Core/Models/Compositeur.cs \
    '&& !char.IsWhiteSpace(texte[j])) j++;' \
    "&& texte[j] != ' ') j++;" \
    'I7-souligne|Souligne(Ecran)?Tests'

saboter "le haut du souligné que donne la police est pris pour son axe" \
    MyPublisher.Core/Models/Souligne.cs \
    'var premier = haut + amplitude + trait / 2;' \
    'var premier = haut + amplitude;' \
    'I7-souligne|Souligne(Ecran)?Tests'

saboter "un souligné épais garde l'épaisseur de la police" \
    MyPublisher.Core/Models/Souligne.cs \
    'var trait = Thick ? 2 * epaisseur : epaisseur;' \
    'var trait = epaisseur;' \
    'I7-souligne|Souligne(Ecran)?Tests'

saboter "le second trait d'un souligné double touche le premier" \
    MyPublisher.Core/Models/Souligne.cs \
    'premier + 2 * amplitude + 2 * trait' \
    'premier + 2 * amplitude + trait' \
    'I7-souligne|Souligne(Ecran)?Tests'

# Repris après relecture : la part couverte ne sépare pas deux motifs voisins, et deux
# motifs échangés restent cinq dessins distincts. La recette lit le dessin — la longueur
# des tirets, les points entre eux —, le test la valeur.
saboter "le tiret-point et le tiret-point-point échangent leurs motifs" \
    MyPublisher.Core/Models/Souligne.cs \
    '        TraitDeSouligne.DotDash => [4, 2, 1, 2],
        TraitDeSouligne.DotDotDash => [4, 2, 1, 2, 1, 2],' \
    '        TraitDeSouligne.DotDash => [4, 2, 1, 2, 1, 2],
        TraitDeSouligne.DotDotDash => [4, 2, 1, 2],' \
    'I7-souligne|Souligne(Ecran)?Tests'

saboter "le tiret et le tiret long échangent leurs motifs" \
    MyPublisher.Core/Models/Souligne.cs \
    '        TraitDeSouligne.Dash => [4, 2],
        TraitDeSouligne.LongDash => [8, 3],' \
    '        TraitDeSouligne.Dash => [8, 3],
        TraitDeSouligne.LongDash => [4, 2],' \
    'I7-souligne|Souligne(Ecran)?Tests'

# Repris après relecture : une police qui ne donne pas la place du trait le posait sur
# la ligne de base. Aucune police de la recette ne le fait : le test seul le voit.
saboter "une police sans place de souligné pose le trait sur la ligne de base" \
    MyPublisher.Core/Models/Souligne.cs \
    '(haut > 0 ? haut : corps / 10, ' \
    '(haut, ' \
    'I7-souligne|Souligne(Ecran)?Tests'

saboter "le souligné en vague se trace droit" \
    MyPublisher.Core/Models/Souligne.cs \
    'public bool Ondule => Line == TraitDeSouligne.Wave;' \
    'public bool Ondule => false;' \
    'I7-souligne|Souligne(Ecran)?Tests'

saboter "le souligné simple s'écrit en objet et non « true »" \
    MyPublisher.Core/Models/Souligne.cs \
    'if (valeur == Souligne.Simple) { ecrivain.WriteBooleanValue(true); return; }' \
    '' \
    'I7-souligne|Souligne(Ecran)?Tests'

# Les deux bibliothèques ne rendent pas la position avec le même signe : Avalonia la
# compte vers le bas, PDFsharp avec le signe du fichier. Chacun des deux rendus a donc
# son sabotage, et c'est la table « post » lue par la recette qui les attrape.
saboter "l'écran lit la position du souligné avec le signe du fichier" \
    MyPublisher/Views/ControleDeBloc.cs \
    'metriques.UnderlinePosition * style.FontSize / em,' \
    '-metriques.UnderlinePosition * style.FontSize / em,' \
    'I7-souligne|Souligne(Ecran)?Tests'

saboter "l'export lit la position du souligné comme l'écran la reçoit" \
    MyPublisher.Core/Services/PdfExporter.cs \
    '-metriques.UnderlinePosition * style.FontSize / em,' \
    'metriques.UnderlinePosition * style.FontSize / em,' \
    'I7-souligne'

saboter "l'export ne trace plus le souligné" \
    MyPublisher.Core/Services/PdfExporter.cs \
    'foreach (var trace in style.Underline.Traits(haut, epaisseur))' \
    'foreach (var trace in Souligne.Aucun.Traits(haut, epaisseur))' \
    'I7-souligne'

# Repris après relecture : rien ne soulignait un exposant, ni un texte de couleur hors
# des lignes justifiées. La recette mesure le trait de « 12 » sous sa ligne de base
# décalée, et la teinte des deux traits sur l'image en couleur.
saboter "l'export pose le souligné d'un exposant sous la ligne de base du texte" \
    MyPublisher.Core/Services/PdfExporter.cs \
    '                        ligneDeBase - etendue.Style.BaselineShift * etendue.Style.FontSize);' \
    '                        ligneDeBase);' \
    'I7-souligne'

saboter "l'écran pose le souligné d'un exposant sous la ligne de base du texte" \
    MyPublisher/Views/TexteCompose.cs \
    'var baseDuMorceau = ligneDeBase - style.BaselineShift * style.FontSize;' \
    'var baseDuMorceau = ligneDeBase;' \
    'I7-souligne'

saboter "l'export trace le souligné en noir sous un texte de couleur" \
    MyPublisher.Core/Services/PdfExporter.cs \
    'var couleur = ParseColor(style.Foreground) ?? XColors.Black;' \
    'var couleur = XColors.Black;' \
    'I7-souligne'

saboter "l'écran trace le souligné en noir sous un texte de couleur" \
    MyPublisher/Views/TexteCompose.cs \
    'var pinceau = Converters.Conv.ToBrush(trait.Couleur) ?? Brushes.Black;' \
    'var pinceau = Brushes.Black;' \
    'I7-souligne'

saboter "l'écran ne trace plus le souligné" \
    MyPublisher/Views/TexteCompose.cs \
    'foreach (var trait in traits) Tracer(contexte, trait);' \
    '' \
    'I7-souligne|Souligne(Ecran)?Tests'

saboter "l'écran trace plein un souligné pointillé" \
    MyPublisher/Views/TexteCompose.cs \
    'motif is null ? null : new DashStyle(motif, 0)' \
    'null' \
    'I7-souligne'

saboter "l'import lit le souligné double en simple" \
    MyPublisher.Core/Services/PubImporter.cs \
    '@double: type == "double",' \
    '@double: false,' \
    'I7-souligne|Souligne(Ecran)?Tests'

saboter "l'import fige le souligné du bloc dans les fragments qui le partagent" \
    MyPublisher.Core/Services/PubImporter.cs \
    'if (fragment.Underline == dominant.Underline) fragment.Underline = null;' \
    '' \
    'I7-souligne|Souligne(Ecran)?Tests'

saboter "une copie de bloc perd son souligné" \
    MyPublisher.Core/Models/Frame.cs \
    'Italic = Italic, Underline = Underline, Foreground = Foreground, Align = Align,' \
    'Italic = Italic, Foreground = Foreground, Align = Align,' \
    'CloneTests|I7-souligne|Souligne(Ecran)?Tests'

saboter "« Souligné » s'emboîte dans la saisie ouverte, sans pas à lui" \
    MyPublisher/ViewModels/MainWindowViewModel.Souligne.cs \
    'EndPendingEdit();' \
    '' \
    'I7-souligne|Souligne(Ecran)?Tests'

# C5b — les jeux de couleurs et de polices. La référence se résout en un seul endroit
# du noyau, que les deux rendus appellent : un sabotage du noyau tombe des deux côtés à
# la fois, et c'est la recette — qui calcule les couleurs attendues depuis les valeurs
# des jeux — ou les tests du noyau qui le voient, jamais la comparaison écran ↔ PDF.
saboter "l'export ne résout plus le départ d'un dégradé du jeu" \
    MyPublisher.Core/Services/PdfExporter.cs \
    'var depart = ParseColor(charte.Couleur(fill.From));' \
    'var depart = ParseColor(fill.From);' \
    'C5b-jeux'

saboter "l'écran peint un fond du jeu sans le résoudre" \
    MyPublisher/Views/FondBloc.cs \
    'if (fond is not GradientFill degrade) return Conv.ToBrush(fond.UniformeSelon(charte));' \
    'if (fond is not GradientFill degrade) return Conv.ToBrush(fond.Uniforme);' \
    'C5b-jeux|JeuxTests'

saboter "un fragment de style du jeu garde sa référence" \
    MyPublisher.Core/Models/Jeux.cs \
    'if (copie.Foreground is { } couleur) copie.Foreground = Couleur(couleur) ?? couleur;' \
    '' \
    'C5b-jeux|JeuxTests'

saboter "une teinte du jeu mélange sa référence et non la couleur du jeu" \
    MyPublisher.Core/Models/FillBrush.cs \
    'var baseResolue = charte is null ? Color : charte.Couleur(Color);' \
    'var baseResolue = Color;' \
    'C5b-jeux|JeuxTests'

saboter "la police du jeu ne se résout plus" \
    MyPublisher.Core/Models/Jeux.cs \
    'FontFamily = Police(style.FontFamily),' \
    'FontFamily = style.FontFamily,' \
    'C5b-jeux|JeuxTests'

saboter "la migration laisse une valeur de la version 4 devenir une référence" \
    MyPublisher.Core/Services/FormatMigrations.cs \
    'objet[nom] = ReferenceDuJeu.Liberer(chaine);' \
    'objet[nom] = chaine;' \
    'JeuxTests'

# Le piège du sélecteur, troisième du nom après C1 et C5c : il renvoie ce qu'il lit.
saboter "le sélecteur renvoie au bloc la couleur du jeu qu'il vient d'afficher" \
    MyPublisher/Views/SelecteurDeCouleur.cs \
    'if (_posee == couleur) return;' \
    '' \
    'JeuxTests|C5b-jeux'

# Le banc rend la page de force : seul un test qui lit la fenêtre composée voit qu'elle
# ne s'est pas repeinte.
saboter "changer de jeu ne repeint plus un fond du jeu à l'écran" \
    MyPublisher/Views/FondBloc.cs \
    'JeuAffiche.CharteProperty.Changed.AddClassHandler<FondBloc>((c, _) => c.InvalidateVisual());' \
    '' \
    'JeuxTests'

saboter "changer de jeu ne repeint plus un texte du jeu à l'écran" \
    MyPublisher/Views/ControleDeBloc.cs \
    'JeuAffiche.CharteProperty.Changed.AddClassHandler<ControleDeBloc>((c, _) => c.InvalidateVisual());' \
    '' \
    'JeuxTests'

# Trouvés par la relecture de C5b : deux trous que les neuf sabotages ci-dessus ne
# couvraient pas.
saboter "la migration ne surveille plus la seconde couleur d'un dégradé" \
    MyPublisher.Core/Services/FormatMigrations.cs \
    '["Color", "From", "To", "Stroke", "Foreground", "FontFamily"];' \
    '["Color", "From", "Stroke", "Foreground", "FontFamily"];' \
    'JeuxTests'

saboter "une pastille du jeu perd l'opacité propre du fond" \
    MyPublisher/Views/SelecteurDeCouleur.cs \
    'Valeur = ReferenceDuJeu.Couleur(emplacement, alpha is { } a && a != 0xFF ? a : null);' \
    'Valeur = ReferenceDuJeu.Couleur(emplacement);' \
    'JeuxTests'

# Q20 : une police absente de la machine survit à la sélection. Deux garde-fous, et
# chacun a son test : la liste qui propose la police du bloc, et la liaison qui ne
# renvoie jamais null au bloc — seule à tenir pour un bloc venu après la liste.
saboter "la liste des polices ne propose plus les polices absentes du document" \
    MyPublisher/ViewModels/MainWindowViewModel.cs \
    '            .. absentes,' \
    '' \
    'PolicesAbsentesTests|Q20-police-absente'

saboter "la liste des polices renvoie null au bloc qu'elle ne sait pas montrer" \
    MyPublisher/Converters/JeuConverters.cs \
    'value ?? BindingOperations.DoNothing;' \
    'value;' \
    'PolicesAbsentesTests|Q20-police-absente'

# Salve 2, lot « noyau » : Q8, Q15 et Q17. Chaque sabotage a été joué à la main contre
# la recette de son poste avant d'être écrit ici.

# Q8 : le défaut d'origine, rétabli tel quel — la passe relit l'héritage des fragments
# pour son compte au lieu des styles composés. Huit des neuf tests de Q8 tombent, et
# la recette, sur le document du plan.
saboter "la passe des polices relit l'héritage des fragments au lieu des styles composés" \
    MyPublisher.Core/Services/PdfExporter.cs \
    'foreach (var style in StylesDessines(bloc, content, charte))' \
    'foreach (var style in bloc.Spans.Select(f => Commun(bloc, charte) with { FontFamily = charte.Police(f.FontFamily ?? bloc.FontFamily), Bold = f.Bold ?? bloc.Bold, Italic = f.Italic ?? bloc.Italic }).Prepend(Commun(bloc, charte)))' \
    'Q8-polices|PolicesTests'

# Q8 : la passe lit les fragments d'un bloc qui montre un champ, que le dessin ignore.
# Seul le test du folio le voit.
saboter "la passe des polices lit les fragments d'un champ, que le dessin ignore" \
    MyPublisher.Core/Services/PdfExporter.cs \
    'content, Fragments(text, content, charte), Commun(text, charte)))' \
    'content, charte.Fragments(text.Spans), Commun(text, charte)))' \
    'Q8-polices|PolicesTests'

# Q8 : la passe ne descend plus dans les cases d'un tableau. Trouvé par la relecture :
# rien ne le gardait. Seul le test de la case le voit.
saboter "la passe des polices ne voit plus les cases d'un tableau" \
    MyPublisher.Core/Services/PdfExporter.cs \
    'TableFrame tableau => tableau.CellFrames,' \
    '' \
    'Q8-polices|PolicesTests'

# Q15 : l'ouverture ne compare plus les empreintes. L'écran garde l'image d'avant la
# retouche, le PDF montre la nouvelle.
saboter "rouvrir un document ne relit plus une image retouchée" \
    MyPublisher/ViewModels/MainWindowViewModel.cs \
    'BitmapCache.OublierLesRetouchees();' \
    '' \
    'Q15-image|ImagesTests'

# Q15 : chaque moitié de l'empreinte a son test. La taille oubliée, seule la retouche
# qui garde sa date passe ; la date oubliée, seule la retouche de même taille passe.
saboter "l'empreinte d'une image oublie la taille de son fichier" \
    MyPublisher/Converters/Conv.cs \
    'new Empreinte(fichier.LastWriteTimeUtc, fichier.Length)' \
    'new Empreinte(fichier.LastWriteTimeUtc, 0)' \
    'Q15-image|ImagesTests'

saboter "l'empreinte d'une image oublie la date de son fichier" \
    MyPublisher/Converters/Conv.cs \
    'new Empreinte(fichier.LastWriteTimeUtc, fichier.Length)' \
    'new Empreinte(default, fichier.Length)' \
    'Q15-image|ImagesTests'

# Q15 : l'empreinte prise APRÈS la lecture. Seul un fichier réécrit pendant qu'on le
# lit le trahit ; le lecteur injecté du test le joue. Trouvé par la relecture.
saboter "l'empreinte d'une image se prend après sa lecture" \
    MyPublisher/Converters/Conv.cs \
    'Cache[cle] = new Entree(bitmap, empreinte);' \
    'Cache[cle] = new Entree(bitmap, Prendre(cle));' \
    'Q15-image|ImagesTests'

# Q17 : l'accesseur des fragments rouvert. Tout compile, rien ne casse à l'écran
# aujourd'hui ; la sonde de la recette compile le remplacement, et la réflexion voit
# la marque « init » disparaître.
saboter "les fragments d'un bloc se remplacent de nouveau après la construction" \
    MyPublisher.Core/Models/Frame.cs \
    'public ObservableCollection<StyleSpan> Spans { get; init; } = [];' \
    'public ObservableCollection<StyleSpan> Spans { get; set; } = [];' \
    'Q17-collections|CollectionsFermeesTests'

# Salve 2, lot « rendu » : Q4, Q5, Q6 et Q16, les écarts entre l'écran et le PDF.
# Chacun éprouvé à la main avant d'être écrit ici : appliqué, la cible tombée, restauré.

# Q4 : l'écran rentrait le filet d'une demi-épaisseur. La marge qui le recentre ôtée,
# le contour commun des blocs rentre de nouveau d'une demi-épaisseur — la recette de Q4
# le voit sur vingt côtés sur quarante-huit (image, textes, groupe), celle de C1,
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
    'C5-Q6-fonds'

saboter "à l'écran, un remplissage par image oublie l'arrondi du bloc" \
    MyPublisher/Views/FondBloc.cs \
    ': (Geometry)new RectangleGeometry(rect, Arrondi, Arrondi);' \
    ': (Geometry)new RectangleGeometry(rect);' \
    'C5-Q6-fonds'

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

# Relecture du lot « rendu », 12 octobre 2026. La marge seule ne centrait que les filets
# dont la demi-épaisseur est un point entier : Avalonia l'arrondissait au point, en
# arrondi bancaire. Rétabli sur l'un des trois éléments du filet, l'arrondi remet les
# filets de 1 et de 3 pt à un demi-point du PDF.
saboter "à l'écran, le contour commun reprend l'arrondi de mise en page" \
    MyPublisher/Views/PageCanvas.axaml \
    $'            <Setter Property="UseLayoutRounding" Value="False"/>\n' \
    '' \
    'Q4-filet'

saboter "à l'écran, le rectangle d'une forme reprend l'arrondi de mise en page" \
    MyPublisher/Views/PageCanvas.axaml \
    $'                           Margin="{Binding StrokeThickness, Converter={x:Static conv:Conv.FiletCentre}}"\n                           UseLayoutRounding="False"' \
    '                           Margin="{Binding StrokeThickness, Converter={x:Static conv:Conv.FiletCentre}}"' \
    'Q4-filet'

saboter "à l'écran, l'ellipse d'une forme reprend l'arrondi de mise en page" \
    MyPublisher/Views/PageCanvas.axaml \
    $'                         Margin="{Binding StrokeThickness, Converter={x:Static conv:Conv.FiletCentre}}"\n                         UseLayoutRounding="False"' \
    '                         Margin="{Binding StrokeThickness, Converter={x:Static conv:Conv.FiletCentre}}"' \
    'Q4-filet|C5-Q6-fonds'

# Le sabotage du relecteur, qui passait toute la vérification : la demi-épaisseur
# arrondie à l'entier inférieur. Les filets de 4 pt restent justes ; ceux de 1 et de
# 3 pt, non.
saboter "la marge qui centre le filet est arrondie à l'entier inférieur" \
    MyPublisher/Converters/Conv.cs \
    'new(epaisseur => new Thickness(-Math.Max(0, epaisseur) / 2));' \
    'new(epaisseur => new Thickness(-Math.Floor(Math.Max(0, epaisseur) / 2)));' \
    'Q4-filet'

# Q5 : un bloc tourné ne compte plus que t/2. La pointe d'onglet de son angle, qui sort
# de t/2·√2, est émoussée par la découpe du groupe.
saboter "la découpe d'un groupe émousse la pointe d'un bloc tourné" \
    MyPublisher.Core/Models/Decoupe.cs \
    'marge = Math.Max(marge, Tourne(bloc) ? propre * Math.Sqrt(2) : propre);' \
    'marge = Math.Max(marge, propre);' \
    'Q5-groupe|DecoupeTests'

# Q9 : la vignette espace ses filets du corps, comme le faisait AbsoluteLineHeight,
# et non de l'avance que la composition donne. Le compte des filets ne bouge pas ;
# leur place, si.
saboter "la vignette espace ses filets du corps et non de la hauteur naturelle" \
    MyPublisher/Views/TemplatePreview.cs \
    'yield return (x, descente + ligne.Haut, run, ligne.Avance);' \
    'yield return (x, descente + ligne.Haut * text.FontSize / metriques.Naturelle(commun), run, ligne.Avance);' \
    'Q9-vignette|VignetteTests'

# Q9, après relecture : le fer et la longueur des filets, la découpe au bloc, le filet
# d'un champ et la mémoire des filets. Chacun passait à travers un test qui ne lisait
# que l'ordonnée des filets du bulletin.
saboter "la vignette perd le fer : chaque filet part du retrait et court sur toute la colonne" \
    MyPublisher/Views/TemplatePreview.cs \
    'var justifiee = ligne.Fer == TextAlign.Justify && !ligne.FinDeParagraphe;' \
    'var justifiee = true;' \
    'Q9-vignette|VignetteTests'

saboter "la vignette ne découpe plus ses filets au bloc" \
    MyPublisher/Views/TemplatePreview.cs \
    'using var _ = context.PushClip(new Rect(text.X, text.Y, text.Width, text.Height));' \
    '' \
    'Q9-vignette|VignetteTests'

saboter "un champ ne laisse plus de filet dans la vignette" \
    MyPublisher/Views/TemplatePreview.cs \
    'yield return (0, 0, width, Compositeur.Avance(metriques.Naturelle(commun), text.LineHeight));' \
    '' \
    'Q9-vignette|VignetteTests'

saboter "la vignette garde les filets du premier document qu'on lui pose" \
    MyPublisher/Views/TemplatePreview.cs \
    'if (!ReferenceEquals(document, _filetsDe) || box != _filetsA)' \
    'if (_filetsDe is null)' \
    'Q9-vignette|VignetteTests'

# La fusion de C5b et de Q9 : la vignette compose et peint ses filets par la charte du
# modèle, comme la page. Les modèles prennent leurs couleurs et leurs polices dans leur
# jeu : sans la charte, un filet n'a plus la couleur du texte, ni ses lignes.
saboter "la vignette peint ses filets sans résoudre la couleur du jeu" \
    MyPublisher/Views/TemplatePreview.cs \
    'var brush = Conv.ToBrush(charte.Couleur(text.Foreground)) ?? Brushes.Black;' \
    'var brush = Conv.ToBrush(text.Foreground) ?? Brushes.Black;' \
    'Q9-vignette|VignetteTests'

saboter "la vignette compose ses filets sans résoudre la police du jeu" \
    MyPublisher/Views/TemplatePreview.cs \
    '        var commun = charte.StyleCommun(text);' \
    '        var commun = new StyleEffectif(text.FontFamily, text.FontSize, text.Bold, text.Italic, text.Underline, text.Foreground, 0);' \
    'Q9-vignette|VignetteTests'

# Le poste B2. Le décodage peut être juste et le branchement faux : deux sabotages
# visent le noyau, trois le modèle de vue. Chacun joué à la main contre la recette du
# poste et ses tests avant d'être écrit ici.
saboter "un fichier Latin-1 est lu en UTF-8 et perd ses accents" \
    MyPublisher.Core/Services/TexteImporte.cs \
    'texte = Windows1252.GetString(octets);' \
    'texte = Encoding.UTF8.GetString(octets);' \
    'B2-import-texte|TexteImporteTests|ImportTexteTests'

saboter "un texte importé garde ses fins de ligne CRLF" \
    MyPublisher.Core/Services/TexteImporte.cs \
    "var lignes = texte.Replace(\"\\r\\n\", \"\\n\").Replace('\\r', '\\n');" \
    'var lignes = texte;' \
    'B2-import-texte|TexteImporteTests|ImportTexteTests'

saboter "importer un texte ne s'annule plus en un pas" \
    MyPublisher/ViewModels/MainWindowViewModel.cs \
    $'        BeginEdit();\n        bloc.Spans.Clear();' \
    $'        bloc.Spans.Clear();' \
    'B2-import-texte|TexteImporteTests|ImportTexteTests'

# Posé par la seule saisie, le texte importé garderait le style du début qu'il partage
# avec l'ancien : un titre gras resterait gras.
saboter "le texte importé garde les fragments d'avant" \
    MyPublisher/ViewModels/MainWindowViewModel.cs \
    $'        bloc.Spans.Clear();\n        bloc.Paragraphs.Clear();\n' \
    '' \
    'B2-import-texte|TexteImporteTests|ImportTexteTests'

saboter "le menu offre l'import à tout bloc sélectionné" \
    MyPublisher/ViewModels/MainWindowViewModel.cs \
    'public bool PeutImporterTexte => SingleSelected is TextFrame { HasField: false };' \
    'public bool PeutImporterTexte => SingleSelected is not null;' \
    'B2-import-texte|TexteImporteTests|ImportTexteTests'

# Après la relecture : l'import pendant une autre édition, et les fichiers qu'on refuse.
saboter "importer pendant la saisie sur la page la laisse ouverte" \
    MyPublisher/Views/MainWindow.axaml.cs \
    $'        PageView.EndTextEdit();\n' \
    '' \
    'B2-import-texte|ImportTexteTests'

saboter "l'import s'emboîte dans la transaction ouverte au panneau" \
    MyPublisher/ViewModels/MainWindowViewModel.cs \
    $'        EndPendingEdit();\n        BeginEdit();\n        bloc.Spans.Clear();' \
    $'        BeginEdit();\n        bloc.Spans.Clear();' \
    'B2-import-texte|ImportTexteTests'

saboter "un fichier UTF-16 petit-boutiste n'est plus reconnu à sa marque" \
    MyPublisher.Core/Services/TexteImporte.cs \
    'else if (octets.StartsWith(MarqueUtf16Petit) || octets.StartsWith(MarqueUtf16Gros))' \
    'else if (octets.StartsWith(MarqueUtf16Gros))' \
    'B2-import-texte|TexteImporteTests'

saboter "un fichier binaire entre dans le bloc, octets nuls compris" \
    MyPublisher.Core/Services/TexteImporte.cs \
    "if (!char.IsControl(c) || c is '\\t' or '\\n' or '\\r' or '\\f') continue;" \
    "if (!char.IsControl(c) || c is '\\t' or '\\n' or '\\r' or '\\f' or '\\0') continue;" \
    'B2-import-texte|TexteImporteTests|ImportTexteTests'

saboter "un code d'échappement entre dans le bloc" \
    MyPublisher.Core/Services/TexteImporte.cs \
    "if (!char.IsControl(c) || c is '\\t' or '\\n' or '\\r' or '\\f') continue;" \
    "if (c != '\\0') continue;" \
    'B2-import-texte|TexteImporteTests'

saboter "un fichier de plusieurs mégaoctets est versé dans le bloc" \
    MyPublisher.Core/Services/TexteImporte.cs \
    'if (flux.Length > TailleMax)' \
    'if (flux.Length > 100 * TailleMax)' \
    'B2-import-texte|TexteImporteTests|ImportTexteTests'

saboter "du Windows-1252 qui passe pour de l'UTF-8 n'est plus relu en Latin-1" \
    MyPublisher.Core/Services/TexteImporte.cs \
    "if (c is >= '\\u0080' and <= '\\u009F') return null;" \
    "if (c is >= '\\u0080' and <= '\\u009F') break;" \
    'B2-import-texte|TexteImporteTests'

saboter "un fichier qui ne porte qu'une fin de ligne efface le bloc" \
    MyPublisher.Core/Services/TexteImporte.cs \
    'if (texte.Length == 0)' \
    'if (texte.Length < 0)' \
    'B2-import-texte|TexteImporteTests'

saboter "un fichier fermé en lecture fait tomber l'import au lieu d'être refusé" \
    MyPublisher/ViewModels/MainWindowViewModel.cs \
    'when (ex is IOException or UnauthorizedAccessException or TexteRefuseException)' \
    'when (ex is IOException or TexteRefuseException)' \
    'B2-import-texte|ImportTexteTests'

# Q7 : la recette de A6 trace ses blocs à l'outil. Les deux sabotages du chemin par
# lequel un bloc naît, que l'ancienne recette — blocs posés à la main, sélectionnés
# par le modèle de vue — laissait passer.
saboter "le tracé ne donne plus sa largeur au bloc" \
    MyPublisher/ViewModels/MainWindowViewModel.cs \
    'frame.Width = width;' \
    '' \
    'A6-Q7-taille|PanneauEtBarreTests'

saboter "le bloc tracé n'est plus sélectionné" \
    MyPublisher/ViewModels/MainWindowViewModel.cs \
    'SelectOnly(frame);' \
    '' \
    'A6-Q7-taille|PanneauEtBarreTests'

# Q14 : les deux causes du double-clic qui réécrivait le bloc, chacune à sa ligne.
saboter "le panneau réécrit la position du bloc qu'on sélectionne" \
    MyPublisher/Converters/UnitConverters.cs \
    'decimal millimetres when _affiches.Retrouver(millimetres, out var points) => points,' \
    '' \
    'Q14-double-clic|DoubleClicTests'

saboter "le panneau arrondit l'angle du bloc qu'on sélectionne" \
    MyPublisher/Converters/UnitConverters.cs \
    'decimal number when _affiches.Retrouver(number, out var exact) => exact,' \
    '' \
    'Q14-double-clic|DoubleClicTests'

saboter "un mouvement nul du pointeur aimante le bloc cliqué" \
    MyPublisher/Views/PageCanvas.axaml.cs \
    'if (dx == 0 && dy == 0)' \
    'if (dx == 0 && dy == 0 && _moveStart.Count < 0)' \
    'Q14-double-clic|DoubleClicTests'

# Q14, repris après relecture : la garde ne fait pas que s'abstenir, elle remet le bloc
# à son départ. Sans cette remise, un geste qui part puis revient au point de prise
# laisse le bloc là où le pointeur l'avait mené ; aucun des trois premiers tests ne le
# voyait.
saboter "un geste revenu au point de prise laisse le bloc où il était allé" \
    MyPublisher/Views/PageCanvas.axaml.cs \
    'frame.X = startX;' \
    '' \
    'Q14-double-clic|DoubleClicTests'

# Q14, repris après relecture : le convertisseur ne se souvient que du nombre qu'il vient
# de donner. Le sabotage remet la mémoire d'avant, un dictionnaire gardé toute la session :
# une frappe y retrouvait la valeur exacte qu'un autre bloc avait affichée.
saboter "une frappe prend la valeur exacte qu'un autre bloc a affichée" \
    MyPublisher/Converters/UnitConverters.cs \
'    private decimal? _affiche;
    private double _valeur;

    public decimal Retenir(decimal affiche, double valeur)
    {
        _affiche = affiche;
        _valeur = valeur;
        return affiche;
    }

    public bool Retrouver(decimal affiche, out double valeur)
    {
        var reconnu = _affiche == affiche;
        valeur = _valeur;
        _affiche = null;
        return reconnu;
    }' \
'    private readonly Dictionary<decimal, double> _valeurs = [];

    public decimal Retenir(decimal affiche, double valeur)
    {
        _valeurs[affiche] = valeur;
        return affiche;
    }

    public bool Retrouver(decimal affiche, out double valeur) => _valeurs.TryGetValue(affiche, out valeur);' \
    'Q14-double-clic|DoubleClicTests'

# Q13 : la décision de renvoi lit la mesure du dessin, comme avant le poste. L'écran et
# le PDF recoupent chacun à sa mesure, et « jamais » change de ligne dans la première
# colonne de l'exemple.
saboter "la coupure se décide sur la mesure de chaque rendu et non sur la plus large" \
    MyPublisher.Core/Models/Compositeur.cs \
    'coupureCourante + metriques.LargeurDeCoupure(mot.Texte.TrimEnd(), mot.Style) > disponible' \
    'largeurCourante + metriques.Largeur(mot.Texte.TrimEnd(), mot.Style) > disponible' \
    'Q13-coupure|CoupureTests|CompositeurTests|MetriquesPdfTests'

# Q13 : l'écran reprend la résolution d'Avalonia, et l'italique absent de DejaVu Sans
# sort de la face qu'il a chargée la première — le gras, dans l'ordre du document.
saboter "l'écran résout ses polices par Avalonia et non par la collection du projet" \
    MyPublisher/Views/ControleDeBloc.cs \
    'PolicesDuDocument.Famille(style.FontFamily),' \
    'new FontFamily(style.FontFamily),' \
    'Q13-coupure|CoupureTests'

# Q13, repris après relecture. L'export sans fenêtre oublie la mesure de l'écran : sur
# le corpus, celle du PDF est presque toujours la plus large, et ce sabotage passait
# vert. La page 3 de recette-coupure.mypub a des blocs où l'écran mesure plus large.
saboter "« --export » coupe à la seule mesure du PDF" \
    MyPublisher/Program.cs \
    'PdfExporter.Export(document, target, MesureDeLEcran(), contacts, date)' \
    'PdfExporter.Export(document, target, null, contacts, date)' \
    'Q13-coupure'

saboter "l'export de l'application coupe à la seule mesure du PDF" \
    MyPublisher/ViewModels/MainWindowViewModel.cs \
    'PdfExporter.Export(Document, path, new Views.MetriquesAvalonia(), Contacts.Active, _horloge())' \
    'PdfExporter.Export(Document, path, null, Contacts.Active, _horloge())' \
    'Q13-coupure|CoupureTests'

# L'écran compose sur la taille du contrôle, arrondie au pixel par la mise en page, et
# non sur celle du bloc : 19 lignes coupées autrement sur les blocs au demi-point.
saboter "l'écran compose sur la largeur arrondie du contrôle" \
    MyPublisher/Views/ControleDeBloc.cs \
    'var largeur = Math.Max(1, bloc.Width - bloc.PaddingLeft - bloc.PaddingRight);' \
    'var largeur = Math.Max(1, Bounds.Width - bloc.PaddingLeft - bloc.PaddingRight);' \
    'Q13-coupure'

# La hauteur de même : un bloc à deux colonnes de 58,3 pt composait sur 59, et gardait
# en colonne 1 une ligne que le PDF passe en colonne 2 (seconde relecture de Q13).
saboter "l'écran compose sur la hauteur arrondie du contrôle" \
    MyPublisher/Views/ControleDeBloc.cs \
    'var hauteur = Math.Max(1, bloc.Height - bloc.PaddingTop - bloc.PaddingBottom);' \
    'var hauteur = Math.Max(1, Bounds.Height - bloc.PaddingTop - bloc.PaddingBottom);' \
    'Q13-coupure'

# L'écran ne dessine plus la dernière ligne d'un bloc de plusieurs lignes. Avant la
# relecture, comparer-mots.sh jetait en silence les mots du PDF sans ligne à l'écran.
saboter "l'écran ne dessine plus la dernière ligne d'un bloc" \
    MyPublisher/Views/TexteCompose.cs \
    'for (var rang = 0; rang < mesure.Lignes.Count; rang++)' \
    'for (var rang = 0; rang < mesure.Lignes.Count - (mesure.Lignes.Count > 1 ? 1 : 0); rang++)' \
    'Q13-coupure'

# Sans la porte privée d'Avalonia, « --export » écrivait un PDF sans texte, code nul.
saboter "« --export » écrit un PDF sans la mesure de l'écran quand la porte manque" \
    MyPublisher/Program.cs \
    'if (PolicesDuDocument.Indisponible is { } motif)' \
    'if (PolicesDuDocument.Indisponible is { } motif && motif.Length < 0)' \
    'Q13-coupure|CoupureTests'

# Sans la porte, l'application adressait encore la collection qu'elle n'a pas montée.
saboter "sans la porte, l'écran adresse une collection qui n'existe pas" \
    MyPublisher/Views/PolicesDuDocument.cs \
    'return Indisponible is null ? new FontFamily($"{Cle}#{nom}") : new FontFamily(nom);' \
    'return new FontFamily($"{Cle}#{nom}");' \
    'Q13-coupure|CoupureTests'

# I1 : l'apparence de l'interface. Aucune vue ne s'en sert encore — seules sa recette et
# ses tests la voient, et chaque sabotage doit tomber sous eux. Chaque intitulé dit
# « l'apparence » : « eprouver-le-juge.sh apparence » les joue tous, et eux seuls.

# Le bouton-icône actif peint du gris de survol : il garde sa taille, son icône, et
# l'apparence ses couleurs déclarées ; la recette le lit contre la feuille de style de la
# maquette, la mesure à l'écran voit qu'il n'est plus plein d'accent.
saboter "le bouton-icône actif de l'apparence n'est plus plein d'accent" \
    MyPublisher/Views/Apparence.axaml \
    '    <Style Selector="ToggleButton.ib:checked /template/ ContentPresenter#PART_ContentPresenter">
        <Setter Property="Background" Value="{StaticResource Accent}"/>' \
    '    <Style Selector="ToggleButton.ib:checked /template/ ContentPresenter#PART_ContentPresenter">
        <Setter Property="Background" Value="{StaticResource Survol}"/>' \
    'I1-apparence|ApparenceTests'

# Un sélecteur nu : tous les boutons de la fenêtre d'aujourd'hui passent à 30 × 30 et
# transparents. La recette le lit dans la source, le test le voit à l'écran.
saboter "un style de l'apparence rhabille tous les boutons de la fenêtre" \
    MyPublisher/Views/Apparence.axaml \
    '<Style Selector="Button.ib, ToggleButton.ib">' \
    '<Style Selector="Button, ToggleButton.ib">' \
    'I1-apparence|ApparenceTests'

# Un tracé qui ne se ferme plus : l'icône se dessine encore, presque pareille — l'angle
# en haut à gauche d'« Enregistrer » s'ouvre. Seule la comparaison à la maquette le voit.
saboter "une icône de l'apparence s'écarte du tracé de la maquette" \
    MyPublisher/Views/Apparence.axaml \
    'Data="M5 4h11l3 3v13H5z"' \
    'Data="M5 4h11l3 3v13H5"' \
    'I1-apparence'

# Une icône partagée : son premier emploi la perdrait au second, sans erreur.
saboter "une icône de l'apparence n'existe qu'en un exemplaire" \
    MyPublisher/Views/Apparence.axaml \
    '<Viewbox x:Key="IconeSelection" x:Shared="False"' \
    '<Viewbox x:Key="IconeSelection"' \
    'I1-apparence|ApparenceTests'

# La même couleur, mais écrite en chiffres dans un style : l'écran ne change pas,
# l'apparence a deux sources pour un gris. Le texte visé porte son sélecteur : un style
# ajouté plus tard au même gris ne le rendra pas ambigu.
saboter "un style de l'apparence écrit sa couleur en chiffres" \
    MyPublisher/Views/Apparence.axaml \
    '<Style Selector="TextBlock.libelle">
        <Setter Property="FontSize" Value="11"/>
        <Setter Property="Foreground" Value="{StaticResource Libelle}"/>' \
    '<Style Selector="TextBlock.libelle">
        <Setter Property="FontSize" Value="11"/>
        <Setter Property="Foreground" Value="#5B6470"/>' \
    'I1-apparence'

# Le même blanc, écrit en abrégé : la forme que la recette ne lisait pas.
saboter "un style de l'apparence écrit son blanc en abrégé" \
    MyPublisher/Views/Apparence.axaml \
    '    <Style Selector="ToggleButton.ib:checked">
        <Setter Property="Foreground" Value="{StaticResource Blanc}"/>' \
    '    <Style Selector="ToggleButton.ib:checked">
        <Setter Property="Foreground" Value="#FFF"/>' \
    'I1-apparence'

# L'accent et son survol échangés : chaque couleur reste déclarée une fois, et les tests,
# qui lisent l'accent dans l'apparence, le retrouvent sur le bouton actif. Seule la
# recette, qui tient le nom de chaque couleur et relit la feuille de la maquette, le voit.
saboter "l'apparence échange l'accent et son survol" \
    MyPublisher/Views/Apparence.axaml \
    '<Color x:Key="CouleurAccent">#2557C7</Color>                <!-- bouton actif, bouton principal, sélection, onglet choisi -->
        <Color x:Key="CouleurAccentSurvol">#1B439C</Color>' \
    '<Color x:Key="CouleurAccent">#1B439C</Color>                <!-- bouton actif, bouton principal, sélection, onglet choisi -->
        <Color x:Key="CouleurAccentSurvol">#2557C7</Color>' \
    'I1-apparence'

# La projection de la sélection à 60 % au lieu de 14 % : aucune vue ne la peint encore.
saboter "la projection de l'apparence n'est plus l'accent à 14 %" \
    MyPublisher/Views/Apparence.axaml \
    '<SolidColorBrush x:Key="Projection" Color="{StaticResource CouleurAccent}" Opacity="0.14"/>' \
    '<SolidColorBrush x:Key="Projection" Color="{StaticResource CouleurAccent}" Opacity="0.6"/>' \
    'I1-apparence'

# Le trait des icônes figé au gris des commandes : au repos rien ne change, mais sur
# l'accent l'icône ne passe plus au blanc — ni à aucune couleur courante.
saboter "le trait d'une icône de l'apparence ne suit plus la couleur courante" \
    MyPublisher/Views/Apparence.axaml \
    '<Style Selector="Path.iconeTrait">
        <Setter Property="Stroke" Value="{Binding $self.(TextElement.Foreground)}"/>' \
    '<Style Selector="Path.iconeTrait">
        <Setter Property="Stroke" Value="{StaticResource Commande}"/>' \
    'I1-apparence|ApparenceTests'

# Le champ reprend la hauteur de Fluent.
saboter "un champ de l'apparence rend 32 px au lieu de 26" \
    MyPublisher/Views/Apparence.axaml \
    '<Style Selector="TextBox.champ, ComboBox.champ, NumericUpDown.champ">
        <Setter Property="Height" Value="26"/>' \
    '<Style Selector="TextBox.champ, ComboBox.champ, NumericUpDown.champ">
        <Setter Property="Height" Value="32"/>' \
    'I1-apparence|ApparenceTests'

# Un rayon de 5 au lieu de 6 : les pixels du coin n'y voient rien, l'un et l'autre
# adoucissant le même pixel. Le test le lit sur le bouton, la recette dans la feuille.
saboter "le bouton-icône de l'apparence perd son rayon de 6" \
    MyPublisher/Views/Apparence.axaml \
    '<Style Selector="Button.ib, ToggleButton.ib">
        <Setter Property="Width" Value="30"/>
        <Setter Property="Height" Value="30"/>
        <Setter Property="MinWidth" Value="0"/>
        <Setter Property="MinHeight" Value="0"/>
        <Setter Property="Padding" Value="0"/>
        <Setter Property="CornerRadius" Value="6"/>' \
    '<Style Selector="Button.ib, ToggleButton.ib">
        <Setter Property="Width" Value="30"/>
        <Setter Property="Height" Value="30"/>
        <Setter Property="MinWidth" Value="0"/>
        <Setter Property="MinHeight" Value="0"/>
        <Setter Property="Padding" Value="0"/>
        <Setter Property="CornerRadius" Value="5"/>' \
    'ApparenceTests'

# Le survol peint d'accent : le bouton survolé passerait pour actif.
saboter "le bouton-icône de l'apparence survolé se peint d'accent" \
    MyPublisher/Views/Apparence.axaml \
    'ToggleButton.ib:pressed /template/ ContentPresenter#PART_ContentPresenter">
        <Setter Property="Background" Value="{StaticResource Survol}"/>' \
    'ToggleButton.ib:pressed /template/ ContentPresenter#PART_ContentPresenter">
        <Setter Property="Background" Value="{StaticResource Accent}"/>' \
    'ApparenceTests'

# Le grisé qui ne grise plus : Annuler et Rétablir (I2) auraient l'air actifs sur un
# historique vide. La maquette ne dessine pas cet état ; seul le test le mesure.
saboter "le bouton-icône grisé de l'apparence garde toute son encre" \
    MyPublisher/Views/Apparence.axaml \
    '<Style Selector="Button.ib:disabled, ToggleButton.ib:disabled">
        <Setter Property="Opacity" Value="0.4"/>' \
    '<Style Selector="Button.ib:disabled, ToggleButton.ib:disabled">
        <Setter Property="Opacity" Value="1"/>' \
    'ApparenceTests'

# Une vue d'avant la phase prend en chiffres le gris des libellés de la maquette, sans
# entrer dans la liste des vues de la phase — le cas d'une planche réécrite qui oublierait
# la liste. Rien ne la cite : seul le relevé des couleurs écrites en dur la trahit.
saboter "une vue hors de la phase prend en chiffres une couleur de l'apparence" \
    MyPublisher/Views/BusinessInfoWindow.axaml \
    'TextWrapping="Wrap" FontSize="11" Foreground="#FF8A929E" Margin="0,0,0,10"/>' \
    'TextWrapping="Wrap" FontSize="11" Foreground="#FF5B6470" Margin="0,0,0,10"/>' \
    'I1-apparence'

# I2 et B7 : la disposition de la fenêtre, et les panneaux qui se replient. Chaque
# intitulé commence par « la fenêtre » : « eprouver-le-juge.sh '^la fenêtre' » les joue
# tous, et eux seuls ; « eprouver-le-juge.sh 'I2|B7' » joue ceux qu'attend la recette
# I2-B7, c'est-à-dire tous sauf le glissé, que la recette de A4 attrape.

# Règle 3 : la barre contextuelle qui se masque sans sélection fait descendre la planche
# de 41 px entre la pression du pointeur et son relâchement.
saboter "la fenêtre masque sa barre contextuelle sans sélection" \
    MyPublisher/Views/MainWindow.axaml \
    '        <views:BarreContextuelle DockPanel.Dock="Top" x:Name="BarreContextuelle"' \
    '        <views:BarreContextuelle DockPanel.Dock="Top" x:Name="BarreContextuelle" IsVisible="{Binding HasSelection}"' \
    'I2-B7-disposition|DispositionTests'

# Règle 2 : un onglet qui n'ouvre plus la transaction au focus. Rien d'autre ne le voit —
# les tests d'avant la phase n'ouvraient pas l'onglet Document pour y taper.
saboter "la fenêtre n'ouvre plus de pas d'annulation à l'onglet Document" \
    MyPublisher/Views/MainWindow.axaml \
    '                    <TabItem x:Name="OngletDocument" Header="Document">
                        <ScrollViewer HorizontalScrollBarVisibility="Disabled"
                                      GotFocus="OnPropertyPanelGotFocus" LostFocus="OnPropertyPanelLostFocus">' \
    '                    <TabItem x:Name="OngletDocument" Header="Document">
                        <ScrollViewer HorizontalScrollBarVisibility="Disabled">' \
    'I2-B7-disposition|DispositionTests'

# La liste montre l'avant en tête : déposer au rang de la liste, et non à son miroir dans
# la page, retourne le geste. C'est le glissé de A4, réécrit à l'envers par I2.
saboter "la fenêtre dépose un bloc tiré au rang de la liste et non à son miroir" \
    MyPublisher/Views/MainWindow.axaml.cs \
    'if (vm.ReorderFrame(frame, pile.Miroir(index))) e.Handled = true;' \
    'if (vm.ReorderFrame(frame, index)) e.Handled = true;' \
    'A4-superposition|PanneauEtBarreTests'

# La vue inversée qui garde le rang de la page pour un bloc ajouté : la liste se trompe
# de ligne jusqu'à la prochaine remise à zéro.
saboter "la fenêtre range un bloc ajouté au mauvais rang de « Blocs de la page »" \
    MyPublisher/ViewModels/PileInversee.cs \
    'new(NotifyCollectionChangedAction.Add, e.NewItems[0], compte - 1 - e.NewStartingIndex),' \
    'new(NotifyCollectionChangedAction.Add, e.NewItems[0], e.NewStartingIndex),' \
    'I2-B7-disposition|DispositionTests'

# B7 : Ctrl+F1 ne replie plus que le panneau Pages.
saboter "la fenêtre ne replie plus le dock sous Ctrl+F1" \
    MyPublisher/ViewModels/MainWindowViewModel.Disposition.cs \
    $'                PanneauPagesVisible = false;\n                DockVisible = false;' \
    '                PanneauPagesVisible = false;' \
    'I2-B7-disposition|DispositionTests'

# B7 : le raccourci quitte le tableau du README — il existerait sans que personne le sache.
saboter "la fenêtre replie ses panneaux sans que le README le dise" \
    README.md \
    $'| `Ctrl+F1` | Replier les panneaux, puis les rendre |\n' \
    '' \
    'I2-B7-disposition'

# Annuler qui ne se grise plus : un bouton qui a l'air de pouvoir et ne fait rien.
saboter "la fenêtre laisse Annuler actif sur un historique vide" \
    MyPublisher/Views/MainWindow.axaml \
    'Command="{Binding UndoCommand}" IsEnabled="{Binding PeutAnnuler}"' \
    'Command="{Binding UndoCommand}"' \
    'I2-B7-disposition|DispositionTests'

# La pastille vide sans sélection : la barre garde sa hauteur, mais ne dit plus « Page ».
saboter "la fenêtre ne dit plus « Page » sans sélection" \
    MyPublisher/ViewModels/MainWindowViewModel.Disposition.cs \
    '        0 => "Page",' \
    '        0 => "",' \
    'I2-B7-disposition|DispositionTests'

# Ce que la phase ne fait pas, dessiné quand même : une section Habillage sans moteur.
saboter "la fenêtre dessine l'Habillage que la phase ne fait pas" \
    MyPublisher/Views/MainWindow.axaml \
    '<TextBlock Text="Fond et contour"/>' \
    '<TextBlock Text="Fond et contour"/><TextBlock Text="Habillage"/>' \
    'I2-B7-disposition'

# Sous la locale C, « 52,5 mm » ne se lit plus : la valeur reste celle d'avant. Avec le
# lecteur d'Avalonia, il devenait 525.
saboter "la fenêtre ne lit plus une longueur saisie à la virgule" \
    MyPublisher/Converters/DispositionConverters.cs \
    "        texte = separateur == \",\" ? texte.Replace('.', ',') : texte.Replace(',', '.');" \
    '' \
    'I2-B7-disposition|DispositionTests'

# Une section fermée qui ne le dit pas au modèle de vue : son contenu reste ouvert, et le
# bloc suivant la rouvre.
saboter "la fenêtre oublie une section fermée" \
    MyPublisher/Views/MainWindow.axaml \
    'IsChecked="{Binding #Root.((vm:MainWindowViewModel)DataContext).SectionCadreOuverte}">' \
    'IsChecked="{Binding #Root.((vm:MainWindowViewModel)DataContext).SectionCadreOuverte, Mode=OneWay}">' \
    'I2-B7-disposition|DispositionTests'

# La marge commune qui n'écrit plus que la marge gauche : les trois autres gardent l'ancienne.
saboter "la fenêtre n'écrit plus la marge commune que d'un côté" \
    MyPublisher/ViewModels/MainWindowViewModel.Disposition.cs \
    $'            bloc.PaddingRight = value;\n            bloc.PaddingTop = value;\n            bloc.PaddingBottom = value;\n' \
    '' \
    'I2-B7-disposition|DispositionTests'

# Le curseur du zoom linéaire : au milieu du rail, 405 % au lieu de 89 %.
saboter "la fenêtre mène le zoom par un curseur linéaire" \
    MyPublisher/Converters/DispositionConverters.cs \
    'value is double zoom && zoom > 0 ? Math.Log(zoom) : AvaloniaPropertyUnset();' \
    'value is double zoom && zoom > 0 ? zoom : AvaloniaPropertyUnset();' \
    'I2-B7-disposition|DispositionTests'

# Repris après relecture. Une case du menu Fenêtre liée dans un seul sens : le clic coche
# « Dock » et le dock reste là.
saboter "la fenêtre lie la case Dock de son menu dans un seul sens" \
    MyPublisher/Views/MainWindow.axaml \
    'IsChecked="{Binding DockVisible, Mode=TwoWay}"' \
    'IsChecked="{Binding DockVisible}"' \
    'I2-B7-disposition|DispositionTests'

# Annuler grisé sur l'historique seul : le bloc a bougé au dock, le pas n'est pas encore
# empilé, et le clic ne referme même pas la saisie.
saboter "la fenêtre grise Annuler pendant une saisie qui a changé le bloc" \
    MyPublisher/Views/MainWindow.axaml \
    'IsEnabled="{Binding PeutAnnuler}"' \
    'IsEnabled="{Binding CanUndo}"' \
    'I2-B7-disposition|DispositionTests'

saboter "la fenêtre ne remarque plus qu'une saisie a changé le bloc" \
    MyPublisher/ViewModels/MainWindowViewModel.Disposition.cs \
    '        if (DocumentSerializer.ToJson(Document) != _editBaseline.Json) RetoucheEnCours = true;' \
    '' \
    'I2-B7-disposition|DispositionTests'

# Ctrl+G pendant une saisie du dock : le gras se fond dans le pas du champ (règle 2).
saboter "la fenêtre fond le gras dans la saisie en cours du dock" \
    MyPublisher/ViewModels/MainWindowViewModel.Disposition.cs \
    '        if (ouvertes > 0) EndPendingEdit();' \
    '' \
    'I2-B7-disposition|DispositionTests'

# B7 : un état d'avant le repli qui survit aux gestes du menu Fenêtre.
saboter "la fenêtre rend au raccourci un état d'avant périmé" \
    MyPublisher/ViewModels/MainWindowViewModel.Disposition.cs \
    $'    partial void OnPanneauPagesVisibleChanged(bool value)\n    {\n        if (!_repliEnCours) _avantLeRepli = null;\n    }' \
    $'    partial void OnPanneauPagesVisibleChanged(bool value)\n    {\n    }' \
    'I2-B7-disposition|DispositionTests'

# B7 : un raccourci qui inverse chaque panneau au lieu de les replier — le dock masqué au
# menu, il montrerait le dock et masquerait Pages.
saboter "la fenêtre inverse chaque panneau sous Ctrl+F1" \
    MyPublisher/ViewModels/MainWindowViewModel.Disposition.cs \
    $'    private void ReplierLesPanneaux()\n    {\n' \
    $'    private void ReplierLesPanneaux()\n    {\n        PanneauPagesVisible = !PanneauPagesVisible;\n        DockVisible = !DockVisible;\n        if (DockVisible || !DockVisible) return;\n' \
    'I2-B7-disposition|DispositionTests'

# Règle 3 : la pastille masquée sans sélection. La barre garde sa hauteur et la pastille
# son texte ; seul ce qu'on voit la trahit.
saboter "la fenêtre masque la pastille sans sélection" \
    MyPublisher/Views/BarreContextuelle.axaml \
    '<Border x:Name="PastilleDeLaSelection" Background=' \
    '<Border x:Name="PastilleDeLaSelection" IsVisible="{Binding HasSelection}" Background=' \
    'I2-B7-disposition|DispositionTests'

# Ce que la phase ne fait pas, caché derrière une touche d'accès, dans un menu dont aucun
# test ne compare les entrées.
saboter "la fenêtre dessine « Ajuster le texte » sous une touche d'accès" \
    MyPublisher/Views/MainWindow.axaml \
    '<MenuItem Header="Ajuster aux _marges" Command="{Binding FitToMarginsCommand}"/>' \
    '<MenuItem Header="Ajuster aux _marges" Command="{Binding FitToMarginsCommand}"/><MenuItem Header="A_juster le texte"/>' \
    'I2-B7-disposition'

# Le menu de la barre aux 32 px de Fluent : le survol s'étire d'un bord à l'autre.
saboter "la fenêtre laisse les menus de sa barre à la hauteur de Fluent" \
    MyPublisher/Views/MainWindow.axaml \
    $'        <Style Selector="Menu.barreDeMenus">\n            <Setter Property="Height" Value="NaN"/>\n            <Setter Property="MinHeight" Value="0"/>\n        </Style>\n' \
    '' \
    'I2-B7-disposition|DispositionTests'

# Un champ grisé du dock au gris de Fluent, qui n'est pas de la maquette.
saboter "la fenêtre grise une zone de texte du gris de Fluent" \
    MyPublisher/Views/Apparence.axaml \
    $'    <Style Selector="TextBox.champ:disabled /template/ Border#PART_BorderElement">\n        <Setter Property="Background" Value="{StaticResource Blanc}"/>\n        <Setter Property="BorderBrush" Value="{StaticResource BordChamp}"/>\n    </Style>\n' \
    '' \
    'I2-B7-disposition|DispositionTests'

# La vignette du fond sans la diagonale de « Aucun » : vide, elle ne dit rien.
saboter "la fenêtre ne montre plus « Aucun » dans la vignette du fond" \
    MyPublisher/Views/MainWindow.axaml \
    '<Border x:Name="VignetteDuFond" Classes="vignette" Classes.aucune="{Binding !HasFill}">' \
    '<Border x:Name="VignetteDuFond" Classes="vignette">' \
    'I2-B7-disposition|DispositionTests'

# L'italique d'Inter, simulé : le « I » devient une barre oblique.
saboter "la fenêtre penche le « I » d'Inter pour l'italique" \
    MyPublisher/Views/MainWindow.axaml \
    '            <Setter Property="FontFamily" Value="Georgia, Noto Serif, Liberation Serif, serif"/>
' \
    '' \
    'I2-B7-disposition|DispositionTests'

# I4 : la planche. Chaque intitulé dit « la planche (I4) » : « eprouver-le-juge.sh I4 », ou
# « planche », les joue tous, et eux seuls. Chacun est attendu par la recette du poste ou
# par ses tests (PlancheTests), qui mesurent l'image que la fenêtre compose.

# La projection lit le cadre du bloc et non son emprise : sur un bloc droit, rien ne
# change ; sur un bloc tourné, la bande tombe à côté, des deux côtés.
saboter "la planche (I4) projette le cadre d'un bloc tourné et non son emprise" \
    MyPublisher/Views/PageCanvas.axaml.cs \
    'Projeter(Extent.Union(frames.Select(frame => frame.Extent)));' \
    'Projeter(Extent.Union(frames.Select(frame => new Extent(frame.X, frame.Y, frame.X + frame.Width, frame.Y + frame.Height))));' \
    'I4-Q10-planche|PlancheTests'

# Les repères repartent du haut de la page, comme quand ils vivaient dans PageRoot : sur la
# page rien ne change, au-dessus d'elle le cyan manque.
saboter "la planche (I4) arrête les repères au haut de la page" \
    MyPublisher/Views/PageCanvas.Planche.cs \
    'Canvas.SetTop(trait, coin.Y);' \
    'Canvas.SetTop(trait, 0);' \
    'I4-Q10-planche|PlancheTests'

# La règle de gauche n'apprend plus où tombe le zéro : elle se gradue depuis son bord, et
# la page qui défile ne l'entraîne plus.
saboter "la planche (I4) ne redit plus son zéro à la règle de gauche" \
    MyPublisher/Views/PageCanvas.Planche.cs \
    'if (Surface.TranslatePoint(page, LeftRuler) is { } gauche) LeftRuler.Origine = gauche.Y;' \
    '' \
    'I4-Q10-planche|PlancheTests'

# La page prend le gris de la planche au lieu du blanc du papier : PageRoot change, ce que
# la confrontation au PDF voit sur les quatre pages.
saboter "la planche (I4) peint la page de son propre gris" \
    MyPublisher/Views/PageCanvas.axaml \
    'Background="{x:Static conv:Conv.Papier}"
                               ClipToBounds="True">' \
    'Background="{StaticResource FondPlanche}"
                               ClipToBounds="True">' \
    'I4-Q10-planche|PlancheTests'

# La bulle ne parle plus pendant un redimensionnement : la barre d'état seule dit la taille.
saboter "la planche (I4) ne montre plus la bulle de dimensions au redimensionnement" \
    MyPublisher/Views/PageCanvas.axaml.cs \
    '_mode == DragMode.Resize) MontrerLaBulle(' \
    '_mode == DragMode.Rotate) MontrerLaBulle(' \
    'I4-Q10-planche|PlancheTests'

# Le cadre de sélection colle au bloc, comme avant le poste : les poignées reviennent sur
# les coins, et tous les gestes passent encore.
saboter "la planche (I4) colle le cadre de sélection au bloc" \
    MyPublisher/Views/PageCanvas.axaml.cs \
    'private const double SelectionGap = 3.25;' \
    'private const double SelectionGap = 0;' \
    'I4-Q10-planche|PlancheTests'

# Le bord tiré rejoint le pointeur au lieu de suivre son chemin : saisie au coin du bloc,
# la poignée marche comme avant ; saisie là où elle se dessine, sur le cadre, elle fait
# sauter le bord de 3,25 px au premier mouvement.
saboter "la planche (I4) fait sauter le bord d'un bloc vers le pointeur qui tire sa poignée" \
    MyPublisher/Views/PageCanvas.axaml.cs \
    'var localX = dx * cos + dy * sin;' \
    'var localX = dx * cos + dy * sin + _startPointer.X - _startX - _startWidth;' \
    'I4-Q10-planche|PlancheTests'

# Les poignées pleines d'accent : le geste ne change pas, le dessin n'est plus celui de la
# maquette.
saboter "la planche (I4) remplit ses poignées d'accent" \
    MyPublisher/Views/PageCanvas.axaml \
    '<Style Selector="Rectangle.poignee, Ellipse.poignee">
            <Setter Property="Fill" Value="{StaticResource Blanc}"/>' \
    '<Style Selector="Rectangle.poignee, Ellipse.poignee">
            <Setter Property="Fill" Value="{StaticResource Accent}"/>' \
    'I4-Q10-planche|PlancheTests'

# La règle ne se gradue plus avant le zéro : elle redevient la règle qui bordait la page.
saboter "la planche (I4) ne gradue plus ses règles avant la page" \
    MyPublisher/Views/Ruler.cs \
    'if (at < 0 || at > longueur) continue;' \
    'if (at < Origine || at > longueur) continue;' \
    'I4-Q10-planche|PlancheTests'

# Les marges reprennent un trait en points, qui suit le zoom : à l'échelle 1, 0,4 px de
# rose étalé sur deux colonnes, et plus un pixel franc.
saboter "la planche (I4) trace les marges d'un trait qui suit le zoom" \
    MyPublisher/Views/PageCanvas.axaml \
    'StrokeThickness="{Binding RenderScale, Converter={x:Static conv:Conv.UnPixel}}"' \
    'StrokeThickness="0.4"' \
    'I4-Q10-planche|PlancheTests'

# La feuille perd son ombre : la page se découpe à plat sur le gris.
saboter "la planche (I4) pose la feuille sans ombre" \
    MyPublisher/Views/PageCanvas.axaml \
    'BoxShadow="0 1 2 0 #1F000000, 0 6 33 0 #1A000000"' \
    'Tag="sans ombre"' \
    'I4-Q10-planche|PlancheTests'

# Un repère lâché à gauche de la page, sur la planche où il se voit désormais, y reste.
saboter "la planche (I4) garde un repère lâché hors de la page" \
    MyPublisher/Views/PageCanvas.axaml.cs \
    'if (_cibleDuRepere < 0 || _cibleDuRepere > limit)' \
    'if (_cibleDuRepere < -1000 || _cibleDuRepere > limit)' \
    'I4-Q10-planche|PlancheTests'

# Repris après relecture (25 octobre 2026) : chacun ferme un trou que la relecture a montré
# par un sabotage qui passait.

# Un outil armé trace sur le gris : un bloc posé hors de la page, que PageRoot rogne tout
# entier, et l'outil désarmé.
saboter "la planche (I4) laisse un outil armé poser un bloc sur le gris" \
    MyPublisher/Views/PageCanvas.axaml.cs \
    'if (!DansLaPage(page)) return;' \
    '' \
    'I4-Q10-planche|PlancheTests'

# Un clic sur le gris prend la part rognée d'un bloc qui déborde de la page.
saboter "la planche (I4) prend sur le gris la part rognée d'un bloc" \
    MyPublisher/Views/PageCanvas.axaml.cs \
    'var hit = DansLaPage(page) ? _vm.CurrentPage.Frames' \
    'var hit = true ? _vm.CurrentPage.Frames' \
    'I4-Q10-planche|PlancheTests'

# La bulle dit la taille de l'emprise : sur un bloc droit, c'est la même ; tourné, non.
saboter "la planche (I4) met dans la bulle la taille de l'emprise" \
    MyPublisher/Views/PageCanvas.axaml.cs \
    'MontrerLaBulle(single.Extent, single.Width, single.Height)' \
    'MontrerLaBulle(single.Extent, single.Extent.Width, single.Extent.Height)' \
    'I4-Q10-planche|PlancheTests'

# La bulle écrit dans la culture invariante : sous LC_ALL=C, c'est la culture courante.
saboter "la planche (I4) écrit la bulle hors de la culture courante" \
    MyPublisher/Views/PageCanvas.Planche.cs \
    'string.Format(CultureInfo.CurrentCulture,' \
    'string.Format(CultureInfo.InvariantCulture,' \
    'I4-Q10-planche|PlancheTests'

# La bulle mesurée encore cachée : à sa première apparition, centrée sur une largeur nulle.
saboter "la planche (I4) mesure la bulle avant de la montrer" \
    MyPublisher/Views/PageCanvas.Planche.cs \
    '_bulle.IsVisible = true;
        _bulle.InvalidateMeasure();
        _bulle.Measure(Size.Infinity);' \
    '_bulle.InvalidateMeasure();
        _bulle.Measure(Size.Infinity);
        _bulle.IsVisible = true;' \
    'I4-Q10-planche|PlancheTests'

# La bulle déjà mesurée ne se remesure pas : centrée sur la largeur du texte d'avant.
saboter "la planche (I4) centre la bulle sur son texte d'avant" \
    MyPublisher/Views/PageCanvas.Planche.cs \
    '_bulle.InvalidateMeasure();' \
    '' \
    'I4-Q10-planche|PlancheTests'

# Un trait de marge d'un point : à l'échelle 1, un pixel aussi ; à l'échelle 2, deux.
saboter "la planche (I4) trace les marges d'un point et non d'un pixel" \
    MyPublisher/Views/PageCanvas.axaml \
    'StrokeThickness="{Binding RenderScale, Converter={x:Static conv:Conv.UnPixel}}"' \
    'StrokeThickness="1"' \
    'I4-Q10-planche|PlancheTests'

# La règle oublie l'échelle : à l'échelle 1, rien ne change ; à l'échelle 2, les
# graduations se tassent et la projection tombe à mi-chemin.
saboter "la planche (I4) gradue ses règles sans l'échelle" \
    MyPublisher/Views/Ruler.cs \
    'public double ToScreen(double points) => Origine + points * Scale;' \
    'public double ToScreen(double points) => Origine + points;' \
    'I4-Q10-planche|PlancheTests'

# Le zéro de la règle du haut repris sur Surface : un pixel avant la page, hors de
# l'échelle 1.
saboter "la planche (I4) prend le zéro de la règle sur ce qui porte la page" \
    MyPublisher/Views/PageCanvas.Planche.cs \
    'if (Surface.TranslatePoint(page, TopRuler) is { } haut)' \
    'if (Surface.TranslatePoint(default, TopRuler) is { } haut)' \
    'I4-Q10-planche|PlancheTests'

# Le repère calculé d'après l'échelle, et non lu sur PageRoot : un pixel avant le bord
# du bloc qu'il marque, à 30 % et à 41 %.
saboter "la planche (I4) place les repères sans lire la page" \
    MyPublisher/Views/PageCanvas.Planche.cs \
    'var place = PageRoot.TranslatePoint(new Point(repere.Position, repere.Position), Surface) ?? default;' \
    'var place = new Point(repere.Position * Scale, repere.Position * Scale);' \
    'I4-Q10-planche|PlancheTests'

# Le repère vertical arrondi sans rattrapage : à 64 %, 127,9999995 tombe en 127.
saboter "la planche (I4) arrondit un repère tombé juste sous l'entier" \
    MyPublisher/Views/PageCanvas.Planche.cs \
    'Canvas.SetLeft(trait, Math.Floor(place.X + Rattrapage));' \
    'Canvas.SetLeft(trait, Math.Floor(place.X));' \
    'I4-Q10-planche|PlancheTests'

# Les nombres de la règle dans la police du système, et non dans celle de l'interface.
saboter "la planche (I4) écrit les nombres des règles dans la police du système" \
    MyPublisher/Views/Ruler.cs \
    'public Typeface Police => new(TextElement.GetFontFamily(this));' \
    'public Typeface Police => Typeface.Default;' \
    'I4-Q10-planche|PlancheTests'

# Les nombres de la règle sans leur signe : « 10 » à gauche de la page.
saboter "la planche (I4) écrit les nombres des règles sans leur signe" \
    MyPublisher/Views/Ruler.cs \
    'Math.Round(millimetres).ToString(CultureInfo.CurrentCulture);' \
    'Math.Abs(Math.Round(millimetres)).ToString(CultureInfo.CurrentCulture);' \
    'I4-Q10-planche|PlancheTests'

# Le repère horizontal s'arrête au bord droit de la page.
saboter "la planche (I4) arrête le repère horizontal au bord droit de la page" \
    MyPublisher/Views/PageCanvas.Planche.cs \
    'trait.Width = etendue.Width;' \
    'trait.Width = _vm!.Document.PageWidth * Scale - coin.X;' \
    'I4-Q10-planche|PlancheTests'

# Deux blocs pris : la projection ne couvre que le premier.
saboter "la planche (I4) ne projette que le premier bloc de la sélection" \
    MyPublisher/Views/PageCanvas.axaml.cs \
    'Projeter(Extent.Union(frames.Select(frame => frame.Extent)));' \
    'Projeter(frames.Count > 0 ? frames[0].Extent : null);' \
    'I4-Q10-planche|PlancheTests'

# Le retrait se décide sur la position aimantée : lâché à 3 pt de la page, sur le gris, le
# repère revient au bord et reste.
saboter "la planche (I4) garde un repère lâché sur le gris tout près de la page" \
    MyPublisher/Views/PageCanvas.axaml.cs \
    'if (_cibleDuRepere < 0 || _cibleDuRepere > limit)' \
    'if (_draggedGuide.Position < 0 || _draggedGuide.Position > limit)' \
    'I4-Q10-planche|PlancheTests'

# Le repère traîné écarte des candidats toute valeur égale à la sienne : aimanté sur le
# bord, il s'en décroche au mouvement suivant.
saboter "la planche (I4) décroche un repère du bord où il vient de s'aimanter" \
    MyPublisher/Views/PageCanvas.axaml.cs \
    'var lui = candidates.LastIndexOf(_draggedGuide.Position);
            if (lui >= 0) candidates.RemoveAt(lui);' \
    'candidates.RemoveAll(valeur => valeur == _draggedGuide.Position);' \
    'I4-Q10-planche|PlancheTests'

# I3 : la barre contextuelle. Posés le 26 octobre 2026, à la relecture qui a suivi la fusion :
# le poste n'en avait aucun. Chaque intitulé dit « la barre contextuelle (I3) » :
# « eprouver-le-juge.sh I3 » les joue tous, et eux seuls. Chacun est attendu par la recette du
# poste ou par ses tests (BarreContextuelleTests).

# Un bouton à bascule ordinaire : « Centrer » sur un texte déjà centré se décoche tout seul.
saboter "la barre contextuelle (I3) laisse un bouton à état s'inverser au clic" \
    MyPublisher/Views/BoutonDEtat.cs \
    $'    protected override void Toggle()\n    {\n    }\n' \
    '' \
    'I3-barre-contextuelle|BarreContextuelleTests'

# Le sélecteur de couleur sans sa transaction : la pastille choisie change le bloc sans pas.
saboter "la barre contextuelle (I3) ouvre le sélecteur de couleur hors de toute transaction" \
    MyPublisher/Views/BarreContextuelle.axaml.cs \
    $'        _couleurOuverte = sender as FlyoutBase;\n        vm.BeginEdit();' \
    '        _couleurOuverte = sender as FlyoutBase;' \
    'I3-barre-contextuelle|BarreContextuelleTests'

# Échap absorbé sans rendre le focus : la lettre suivante s'écrit encore dans le corps.
saboter "la barre contextuelle (I3) garde le champ sous Échap" \
    MyPublisher/Views/BarreContextuelle.axaml.cs \
    $'        focus.ClearFocus();\n        e.Handled = true;' \
    '        e.Handled = true;' \
    'I3-barre-contextuelle|BarreContextuelleTests'

# Le bouton « Centrer le texte » qui aligne à gauche : le texte ne bouge pas, ni à l'écran ni
# au PDF.
saboter "la barre contextuelle (I3) aligne à gauche sous « Centrer le texte »" \
    MyPublisher/Views/BarreContextuelle.axaml \
    'CommandParameter="{x:Static models:TextAlign.Center}"/>' \
    'CommandParameter="{x:Static models:TextAlign.Left}"/>' \
    'I3-barre-contextuelle|BarreContextuelleTests'

# Règle 1 : l'interligne monté dans la barre, et resté au dock — deux contrôles pour une
# propriété.
saboter "la barre contextuelle (I3) laisse l'interligne au dock" \
    MyPublisher/Views/MainWindow.axaml \
    '<TextBlock Classes="libelle libelleAuDessus" Text="Retrait à gauche"/>' \
    '<TextBlock Classes="libelle libelleAuDessus" Text="Retrait à gauche"/><NumericUpDown Classes="champ" TextConverter="{x:Static conv:Unite.Multiple}" Increment="0.05" Minimum="0.5" Value="{Binding LineHeight, Converter={x:Static conv:DoubleToDecimal.Instance}}"/>' \
    'I3-barre-contextuelle|BarreContextuelleTests'

# Les commandes de plusieurs blocs montrées pour un seul : « Répartir » sur un bloc de texte.
saboter "la barre contextuelle (I3) montre les commandes de plusieurs blocs pour un seul" \
    MyPublisher/Views/BarreContextuelle.axaml \
    'IsVisible="{Binding HasMultipleSelection}">' \
    'IsVisible="{Binding HasSelection}">' \
    'I3-barre-contextuelle|BarreContextuelleTests'

# Repris après relecture (26 octobre 2026). Une commande qui prend le focus : la transaction de
# la barre s'ouvre, l'alignement y attend son pas, et la flèche frappée ensuite s'y fond.
saboter "la barre contextuelle (I3) laisse ses commandes prendre le focus" \
    MyPublisher/Views/BarreContextuelle.axaml \
    $'        <Style Selector="Button.menu">\n            <Setter Property="Focusable" Value="False"/>\n' \
    $'        <Style Selector="Button.menu">\n' \
    'I3-barre-contextuelle|BarreContextuelleTests'

# La commande sans focus qui ne le reprend pas au champ qui l'a : la marge saisie au dock et
# l'alignement ne font qu'un pas.
saboter "la barre contextuelle (I3) fond la saisie d'un champ dans la commande qui suit" \
    MyPublisher/Views/BarreContextuelle.axaml.cs \
    $'        if (e.Source is not Button bouton || !bouton.Classes.Contains("menu")) return;\n\n        TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();' \
    '        if (e.Source is not Button bouton || !bouton.Classes.Contains("menu")) return;' \
    'I3-barre-contextuelle|BarreContextuelleTests'

# « Souligné » offert au menu Texte sur une forme, où il ne fait rien.
saboter "la barre contextuelle (I3) offre le souligné sans bloc de texte" \
    MyPublisher/ViewModels/MainWindowViewModel.Souligne.cs \
    '[RelayCommand(CanExecute = nameof(EstUnBlocDeTexte))]' \
    '[RelayCommand]' \
    'I3-barre-contextuelle|BarreContextuelleTests'

# Q23 : les réglages du bloc — les trois sections du dock, la barre contextuelle — sont
# construits une fois par sorte de bloc et gardés. Reconstruits à chaque sélection, ils
# figeaient la pression : c'est la cause du poste. Le chronomètre la verrait sur une machine
# calme ; le compte des champs la voit sur toutes.
#
# Repris à la relecture du 26 octobre 2026. Le garde-fou attendu était « Q23-selection ou
# PressionTests » : le chronomètre en est, et la recette le joue. Sous une charge de 18 sur
# vingt cœurs, il tombe aussi sur le code juste — médiane de 104 à 181 ms sur un bloc,
# deux exécutions sur trois —, et il attrapait ainsi deux sabotages qui ne changent rien au
# temps : le juge les comptait pris. Le garde-fou attendu nomme désormais les tests qui
# comptent, et non le chronomètre ni la recette qui l'embarque. Pour celui-ci, les tests de
# la barre en sont : le juge ne nomme que les quatre premiers tests tombés, et ceux de la
# barre, qui tombent aussi et ne lisent pas l'horloge, passent avant PressionTests.
saboter "les réglages du bloc (Q23) se reconstruisent à chaque sélection" \
    MyPublisher/Views/GabaritsGardes.cs \
    'if (gabarit is not null && !_construits.TryGetValue(gabarit, out montre))' \
    'if (gabarit is not null)' \
    'PressionTests\.Les_reglages_du_bloc|BarreContextuelleTests'

saboter "les réglages du bloc (Q23) d'une autre sorte restent visibles" \
    MyPublisher/Views/GabaritsGardes.cs \
    'if (!ReferenceEquals(reglages, montre)) reglages.IsVisible = false;' \
    '' \
    'PressionTests\.(Les_reglages_du_bloc|Un_champ_masque)'

saboter "les réglages gardés (Q23) restent liés au bloc d'avant" \
    MyPublisher/Views/GabaritsGardes.cs \
    'montre.DataContext = bloc;
        montre.IsVisible = true;' \
    'montre.IsVisible = true;' \
    'PressionTests\.Les_reglages_du_bloc|BarreContextuelleTests'

saboter "le sélecteur de couleur (Q23) reste ouvert quand le bloc change sans clic" \
    MyPublisher/Views/BarreContextuelle.axaml.cs \
    'if (e.Property == GabaritsGardes.BlocProperty) FermerLaCouleur();' \
    '' \
    'BarreContextuelleTests\.Changer_de_bloc_sans_le_pointeur'

saboter "le sélecteur de couleur refermé (Q23) rend le focus à un champ gardé de la barre" \
    MyPublisher/Views/BarreContextuelle.axaml.cs \
    'focus.ClearFocus();
        }

        (DataContext as MainWindowViewModel)?.CommitEdit();' \
    '}

        (DataContext as MainWindowViewModel)?.CommitEdit();' \
    'BarreContextuelleTests'

# N1 : les gestes des tests d'interface sont synthétisés par Rendu.cs seul. Un glissé qui ne
# dit pas le bouton tenu était le défaut de deux des trois copies : un TextBox n'y étend pas sa
# sélection, et le banc le voit ; une entrée en saisie qui ne clique qu'une fois n'ouvre rien,
# et l'aide le dit au lieu de laisser le test taper ailleurs ; une aide de classe qui synthétise
# de nouveau sa touche ne change rien aux tests — seul le contrôle des sources la voit.
saboter "le glissé du banc (N1) ne dit plus le bouton tenu" \
    MyPublisher.Tests.Interface/Rendu.cs \
    'racine.MouseMove(ici, touches | RawInputModifiers.LeftMouseButton);' \
    'racine.MouseMove(ici, touches);' \
    'N1-banc-des-gestes|BancTests'

saboter "l'entrée en saisie du banc (N1) ne clique qu'une fois" \
    MyPublisher.Tests.Interface/Rendu.cs \
    'DoubleCliquer(fenetre, SurLaPage(fenetre, bloc.X + dx, bloc.Y + dy));' \
    'Cliquer(fenetre, SurLaPage(fenetre, bloc.X + dx, bloc.Y + dy));' \
    'N1-banc-des-gestes|BancTests'

saboter "une classe de tests (N1) synthétise de nouveau sa touche hors du banc" \
    MyPublisher.Tests.Interface/FrappeTests.cs \
    '        Rendu.Touche(fenetre, PhysicalKey.Escape);
        Assert.False(bloc.IsEditing, "Échap n'"'"'a pas refermé la saisie");' \
    '        fenetre.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Rendu.Battre();
        Assert.False(bloc.IsEditing, "Échap n'"'"'a pas refermé la saisie");' \
    'N1-banc-des-gestes'

# N2 : la mise en forme d'une plage ramène au bloc toute valeur qui lui est égale. Oubliée,
# la normalisation laisse « Bold = false » sur un bloc maigre : le gras rebasculé laisse un
# fragment qui ne dit rien de visible, et deux voisins pareils à l'œil ne se recollent plus.
saboter "la mise en forme (N2) oublie de ramener au bloc une valeur qui lui est égale" \
    MyPublisher.Core/Models/MiseEnForme.cs \
    'surcharges => surcharges.Poser(style).Normaliser(commun),' \
    'surcharges => surcharges.Poser(style),' \
    'N2-mise-en-forme|MiseEnFormeTests'

# N2 : un fragment coupé à une borne laisse ses bouts du dehors en copies. Retouché sur
# place, il ne passe plus par la collection — « ControleDeBloc » ne le redessinerait pas —,
# et l'annulation de R7 n'aurait plus d'objet d'avant à rendre.
saboter "la mise en forme (N2) coupe un fragment en le retouchant sur place" \
    MyPublisher.Core/Models/MiseEnForme.cs \
    'protected override StyleSpan Morceau(StyleSpan modele, int debut, int fin)
        {
            var morceau = modele.Clone();' \
    'protected override StyleSpan Morceau(StyleSpan modele, int debut, int fin)
        {
            var morceau = modele;' \
    'N2-mise-en-forme|MiseEnFormeTests'

# N4 : F9 bascule entre l'affichage courant et la taille réelle, et le second F9 rend
# l'affichage d'avant. Oublié, le second F9 n'a rien à rendre et la page reste à 100 % ;
# rendu sans son défilement, le zoom revient, mais pas ce qu'on regardait.
saboter "F9 (N4) ne rend pas l'affichage d'avant" \
    MyPublisher/ViewModels/MainWindowViewModel.Affichage.cs \
    '_affichageDAvant = (zoom, demande.DefilementDAvant);' \
    '_affichageDAvant = null;' \
    'N4-affichage-au-clavier|AffichageTests'
saboter "le second F9 (N4) rend le zoom d'avant sans son défilement" \
    MyPublisher/ViewModels/MainWindowViewModel.Affichage.cs \
    'DemanderLeZoom(avant.Zoom, avant.Defilement);' \
    'DemanderLeZoom(avant.Zoom);' \
    'N4-affichage-au-clavier|AffichageTests'

# N3 : chaque morceau composé porte la position de son premier caractère. Le renvoi rogne la
# tête blanche du mot renvoyé, qui reste au bout de la ligne qu'il quitte : le morceau
# commence après elle. Qu'il garde la position de son blanc de tête, et son texte n'est plus
# le texte à sa position — la sortie du compositeur, elle, ne bouge pas : seuls les tests du
# poste le voient.
saboter "un mot renvoyé garde la position de son blanc de tête (N3)" \
    MyPublisher.Core/Models/Compositeur.cs \
    'courante.Add(new Morceau(debut, mot.Style, largeurCourante, l, renvoi));' \
    'courante.Add(new Morceau(debut, mot.Style, largeurCourante, l, mot.Debut));' \
    'N3-position-des-morceaux|OrigineDesMorceauxTests|PositionDesMorceauxTests'

# Q24 : Annuler et Rétablir referment toutes les transactions ouvertes, puis lèvent un avis, et
# chaque propriétaire encore actif rouvre la sienne. Sans l'avis, Ctrl+Z sans rien à annuler, ou
# Ctrl+Y après une frappe, laissent la saisie hors de toute transaction : la frappe suivante ne
# s'annule plus, et ne marque pas le document.
saboter "Annuler et Rétablir (Q24) ne lèvent plus l'avis" \
    MyPublisher/ViewModels/MainWindowViewModel.Transactions.cs \
    'private void AviserLesProprietaires() => TransactionsRefermees?.Invoke(this, EventArgs.Empty);' \
    'private void AviserLesProprietaires() { }' \
    'Q24-Q25-transactions|TransactionsDeSaisieTests'

# Le piège du poste : rouvrir à l'aveugle. Une annulation qui relit le document referme la
# saisie par le changement de sélection ; la transaction rouverte pour elle reste sans
# propriétaire, et le glissé suivant y fond son pas.
saboter "la planche (Q24) rouvre la transaction d'une saisie refermée" \
    MyPublisher/Views/PageCanvas.axaml.cs \
    'if (ZoneDeLaSaisieAuFocus()) OuvrirLaTransactionDeLaSaisie();' \
    '_vm?.BeginEdit();' \
    'Q24-Q25-transactions|TransactionsDeSaisieTests'

# Le champ qui garde le focus sous un Ctrl+Z sans rien à annuler croit sa transaction ouverte :
# ce qu'on y saisit ensuite n'entre dans aucun pas.
saboter "le champ (Q24) qui garde le focus ne rouvre plus sa transaction" \
    MyPublisher/Views/MainWindow.axaml.cs \
    'if (_panneauDuChamp is not { IsKeyboardFocusWithin: true }) return;' \
    'return;' \
    'Q24-Q25-transactions|TransactionsDeSaisieTests'

# Le bouton toujours tenu, le glissé repris après un Ctrl+Z sans rien à annuler ne fait aucun pas.
saboter "le glissé (Q24) ne rouvre plus sa transaction" \
    MyPublisher/Views/PageCanvas.axaml.cs \
    'if (_mode is DragMode.Move or DragMode.Resize or DragMode.Rotate or DragMode.Guide or DragMode.Extremite)' \
    'if (_mode is DragMode.Pan)' \
    'Q24-Q25-transactions|TransactionsDeSaisieTests'

# Le sélecteur de couleur resté ouvert sous un Ctrl+Z : la pastille choisie ensuite ne fait aucun pas.
saboter "le sélecteur de couleur (Q24) ne rouvre plus sa transaction" \
    MyPublisher/Views/BarreContextuelle.axaml.cs \
    'if (_couleurOuverte is not null) _vm?.BeginEdit();' \
    '_ = _couleurOuverte;' \
    'Q24-Q25-transactions|TransactionsDeSaisieTests'

# Q25 : la transaction de la saisie suit le focus de sa zone. Refermée seulement à la fin de la
# saisie, elle court du premier focus à Échap, comme avant : le champ de la barre ou du dock qui
# prend le focus pendant la frappe y emboîte la sienne, et son réglage se fond dans le pas de la
# frappe.
saboter "la transaction de la saisie (Q25) court du début de la saisie à sa fin, sans suivre le focus" \
    MyPublisher/Views/PageCanvas.axaml.cs \
    'else FermerLaTransactionDeLaSaisie();' \
    'else { }' \
    'Q24-Q25-transactions|TransactionsDeSaisieTests'

# Le focus rendu à la zone d'un clic dans le texte ne rouvre plus rien : la frappe qui reprend
# n'entre dans aucun pas.
saboter "la zone de saisie (Q25) qui reprend le focus ne rouvre plus sa transaction" \
    MyPublisher/Views/PageCanvas.axaml.cs \
    'if (focus) OuvrirLaTransactionDeLaSaisie();' \
    'if (focus) { }' \
    'Q24-Q25-transactions|TransactionsDeSaisieTests'

# Q24 et Q25 ensemble : sous un Ctrl+Z sans rien à annuler, la saisie rouvre sa transaction
# parce qu'elle est toujours ouverte, et non parce que sa zone a le focus. Le champ de la barre
# qui l'a rouvre aussi la sienne, qui s'emboîte : le corps se fond dans la frappe qui reprend.
saboter "la saisie (Q25) rouvre sa transaction sous l'avis sans que sa zone ait le focus" \
    MyPublisher/Views/PageCanvas.axaml.cs \
    'if (ZoneDeLaSaisieAuFocus()) OuvrirLaTransactionDeLaSaisie();' \
    'if (_editing is not null) OuvrirLaTransactionDeLaSaisie();' \
    'Q24-Q25-transactions|TransactionsDeSaisieTests'

# Le volet de couleur rend le focus à la zone de saisie avant de dire qu'il s'est refermé : la
# transaction de la saisie, rouverte à ce focus, s'emboîte dans celle de la couleur, et la
# pastille se fond dans le pas de la frappe qui suit.
saboter "le sélecteur de couleur (Q25) referme sa transaction après avoir rendu le focus" \
    MyPublisher/Views/BarreContextuelle.axaml.cs \
    'private void SurCouleurQuiSeFerme(object? sender, CancelEventArgs e) => RefermerLaTransactionDeLaCouleur(sender);' \
    'private void SurCouleurQuiSeFerme(object? sender, CancelEventArgs e) { }' \
    'Q24-Q25-transactions|TransactionsDeSaisieTests'

# I5 : la vignette d'une page suit le document ouvert, qui se modifie en place. Le poste
# n'avait aucun sabotage (règle 11) ; la relecture du 26 octobre 2026 lui en donne cinq, un
# par promesse que seul son filet garde.

# Elle écoute chaque bloc de sa page : sourde, elle montre la page d'avant la retouche.
saboter "la vignette d'une page (I5) n'écoute plus ses blocs" \
    MyPublisher/Views/VignetteDePage.cs \
    'bloc.PropertyChanged += SurBloc;' \
    '' \
    'I5-vignettes|VignettesDePagesTests'

# Un bloc retouché fait recomposer ses filets ; redessinés de mémoire, le texte reste où il
# était.
saboter "la vignette d'une page (I5) redessine un bloc retouché sans le recomposer" \
    MyPublisher/Views/VignetteDePage.cs \
    'else Oublier(deLaPage);' \
    'else InvalidateVisual();' \
    'I5-vignettes|VignettesDePagesTests'

# Une page vit aussi longtemps que le document : une vignette sortie de l'arbre qui
# l'écouterait encore resterait accrochée à elle.
saboter "la vignette d'une page (I5) écoute encore sa page hors de l'arbre" \
    MyPublisher/Views/VignetteDePage.cs \
    '_dansLArbre = false;
        Lacher();' \
    '_dansLArbre = false;' \
    'I5-vignettes|VignettesDePagesTests'

# La page courante est bordée de 2 px d'accent ; d'un seul, rien ne la distingue que sa couleur.
saboter "la vignette de la page courante (I5) n'a plus qu'un pixel d'accent" \
    MyPublisher/Views/EntreeDePage.axaml \
    '<Setter Property="BorderThickness" Value="2"/>' \
    '<Setter Property="BorderThickness" Value="1"/>' \
    'I5-vignettes|VignettesDePagesTests'

# Un Ctrl + clic sur la vignette courante vidait la sélection de la liste, et la fenêtre
# restait sans page courante.
saboter "la liste des pages (I5) se laisse vider par un Ctrl + clic" \
    MyPublisher/Views/MainWindow.axaml \
    'SelectionMode="AlwaysSelected"' \
    'SelectionMode="Single"' \
    'I5-vignettes|VignettesDePagesTests'

# Un motif qui ne désigne aucun sabotage ne prouve rien. « eprouver-le-juge.sh I4 » sortait
# en 0 sur « 0 sabotages, 0 attrapés », quand les intitulés de la planche ne portaient pas
# encore le code du poste : le filet passait pour éprouvé sans qu'on l'ait troué.
if [ -n "$filtre" ] && [ "$rang" = 0 ]; then
    echo "ÉCHEC : le motif « $filtre » ne désigne aucun sabotage." >&2
    exit 1
fi

echo
# Rien d'éprouvé n'est pas une épreuve réussie : un motif qui ne retient aucun sabotage — une
# faute de frappe, un motif qui cherche le code du poste là où il n'est pas — ou une tranche
# vide sortait en vert sur « 0 sabotages, 0 attrapés ».
if [ "$sabotages" = 0 ]; then
    portee=""
    [ -n "$filtre" ] && portee=" par le motif « $filtre »"
    [ "$tranches" != 1 ] && portee="$portee dans la tranche $tranche/$tranches ($rang retenus en tout)"
    echo "ÉCHEC : aucun sabotage retenu$portee — rien n'a été éprouvé." >&2
    exit 1
fi
if [ "$cibles" = 1 ]; then
    if [ "$trous" = 0 ]; then
        echo "Les $sabotages textes visés sont présents, chacun une seule fois."
        exit 0
    fi
    echo "CIBLES À REPRENDRE : $trous sur $sabotages." >&2
    exit 1
fi
if [ "$trous" = 0 ]; then
    echo "Le juge sait échouer : $sabotages sabotages, $sabotages attrapés."
else
    echo "TROUS DANS LE FILET : $trous sabotage(s) sur $sabotages non détecté(s)." >&2
    echo "Un sabotage non détecté est une régression que la boucle laisserait passer." >&2
    exit 1
fi
