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
#   Scripts/eprouver-le-juge.sh --cibles   # sans rien saboter : chaque texte visé est-il
#                                          # présent, et une seule fois ? (quelques secondes)
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
    if [ -n "$filtre" ] && ! grep -qE "$filtre" <<< "$intitule"; then return; fi
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
    '        var commun = new StyleEffectif(text.FontFamily, text.FontSize, text.Bold, text.Italic, text.Foreground, 0);' \
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

echo
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
