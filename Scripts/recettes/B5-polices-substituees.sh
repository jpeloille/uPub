#!/usr/bin/env bash
#
# Recette du poste B5 — avertir sur les substitutions de polices.
#
# Ce qu'elle éprouve :
#
#   1. un document dont les polices existent n'avertit de rien ;
#   2. une police dont fontconfig annonce plusieurs noms n'avertit pas davantage ;
#   3. un document réclamant une police absente produit un avertissement qui nomme
#      LES DEUX polices — celle qu'on demandait et celle qu'on a eue ;
#   4. la police employée qu'annonce l'avertissement est bien celle que le système
#      rendra, vérifiée en interrogeant fc-match directement ;
#   5. un fragment de style compte autant qu'un bloc : c'est le cas réel des fiches
#      ATR, où le bloc est en Calibri et seule la flèche est en Wingdings ;
#   6. trois blocs dans la même police absente ne font qu'un avertissement.
#
# Les deux premiers points ne sont pas des formalités. Un avertissement qui crie au
# loup sur des polices installées s'apprend à ne plus être lu — et fontconfig annonce
# toutes les variantes d'une famille d'un coup (« DejaVu Sans,DejaVu Sans Condensed »,
# « Malgun Gothic,맑은 고딕 »), ce qui ferait passer pour des substitutions 81 polices
# présentes de cette machine si on comparait les chaînes au lieu d'y chercher le nom
# demandé. Le point 2 va donc chercher une de ces familles-là sur la machine qui joue
# la recette, plutôt que d'en écrire une en dur qui n'y serait pas installée.
#
# Pourquoi cela mérite une recette : 119 des 329 fragments des fiches de limitations
# et procédures d'urgence sont en Wingdings ou Wingdings 3. Ce sont les flèches. Une
# substitution muette n'y abîme pas l'esthétique, elle remplace « descendre » par une
# lettre au milieu d'une procédure d'urgence.
#
set -euo pipefail
export LC_ALL=C

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$racine"

binaire="MyPublisher/bin/Debug/net10.0/MyPublisher"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# Les avertissements sortent sur la sortie d'erreur, préfixés « Avertissement : ».
polices_signalees() {
    if ! "$binaire" "$1" --export "$travail/sortie.pdf" > /dev/null 2> "$travail/erreur.txt"; then
        echo "ÉCHEC : export impossible ($1)." >&2
        cat "$travail/erreur.txt" >&2
        exit 1
    fi

    grep '^Avertissement : Police' "$travail/erreur.txt" || true
}

# 1 — un document dont les polices existent n'avertit de rien.
#
# C'est « Samples/exemple-bulletin.mypub », celui-là même dont Scripts/reference.txt
# fige le relevé : s'il se mettait à porter un avertissement, le contrôle P2 tomberait.
muet="$(polices_signalees Samples/exemple-bulletin.mypub)"
if [ -n "$muet" ]; then
    echo "ÉCHEC : le document de référence n'emploie que DejaVu, il ne doit rien signaler." >&2
    echo "$muet" >&2
    exit 1
fi

# 2 — une famille aux noms multiples n'est pas une substitution.
#
# On en cherche une sur la machine, en s'en tenant aux noms de lettres, chiffres,
# espaces, points et traits — un nom localisé ou portant « & » ne survivrait ni au sed
# ci-dessous ni au JSON. Puis on demande à fc-match si elle est réellement là, et dans
# un fichier que l'export sait lire : sinon il n'y a rien à éprouver ici, et la recette
# passe son chemin plutôt que d'inventer un échec.
multiple=""
if command -v fc-list > /dev/null && command -v fc-match > /dev/null; then
    multiple="$(fc-list -f '%{family}\n' | grep -E '^[A-Za-z0-9 .,-]+,[A-Za-z0-9 .,-]+$' \
        | sort -u | head -1 | cut -d, -f2)"
    if [ -n "$multiple" ]; then
        rendue="$(fc-match -f '%{family}' "$multiple:weight=80:slant=0")"
        fichier="$(fc-match -f '%{file}' "$multiple:weight=80:slant=0")"
        case ",$rendue," in
            *",$multiple,"*) ;;
            *) multiple="" ;;
        esac

        # Une collection .ttc est bien installée et pourtant substituée, PDFsharp ne
        # sachant pas y choisir : ce n'est pas le cas qu'on éprouve ici.
        if [ "${fichier##*.}" = ttc ]; then multiple=""; fi
    fi
fi

if [ -n "$multiple" ]; then
    sed -e "s|Police Absente De Cette Machine|$multiple|" \
        -e "s|Seconde Police Absente|$multiple|" \
        Samples/recette-polices.mypub > "$travail/presente.mypub"

    criee="$(polices_signalees "$travail/presente.mypub")"
    if [ -n "$criee" ]; then
        echo "ÉCHEC : « $multiple » est installée, fontconfig l'annonce sous plusieurs" >&2
        echo "noms, et l'export la dit substituée." >&2
        echo "$criee" >&2
        exit 1
    fi
fi

# 3 — un document réclamant deux polices absentes les signale toutes les deux.
#
# « Samples/recette-polices.mypub » porte trois blocs dans une même police absente,
# un bloc dans une police présente, et un cinquième dont un seul fragment réclame une
# seconde police absente.
signale="$(polices_signalees Samples/recette-polices.mypub)"
compte="$(printf '%s\n' "$signale" | grep -c . || true)"

if [ "$compte" != 2 ]; then
    echo "ÉCHEC : deux polices absentes attendues, $compte avertissement(s) obtenu(s)." >&2
    echo "$signale" >&2
    echo "Trois blocs partageant une police absente ne doivent en faire qu'un ;" >&2
    echo "un fragment dans une police absente doit en faire un." >&2
    exit 1
fi

for demandee in "Police Absente De Cette Machine" "Seconde Police Absente"; do
    ligne="$(printf '%s\n' "$signale" | grep -F "« $demandee »" || true)"
    if [ -z "$ligne" ]; then
        echo "ÉCHEC : « $demandee » n'est pas nommée dans les avertissements." >&2
        echo "$signale" >&2
        exit 1
    fi

    # 4 — la police employée est nommée, et c'est bien celle du système.
    employee="$(printf '%s\n' "$ligne" \
        | sed -n 's/.* demandée, « \(.*\) » employée.*/\1/p')"

    if [ -z "$employee" ]; then
        echo "ÉCHEC : l'avertissement nomme la police demandée mais pas celle employée." >&2
        echo "$ligne" >&2
        exit 1
    fi

    if [ "$employee" = "$demandee" ]; then
        echo "ÉCHEC : « $demandee » ne peut pas être remplacée par elle-même." >&2
        exit 1
    fi

    # Le contre-appel : on redemande au système, sans passer par notre code. Une
    # substitution annoncée qui ne serait pas celle que fontconfig rend serait pire
    # que pas d'avertissement du tout. Le premier nom suffit : fontconfig les rend
    # tous, séparés par des virgules.
    if command -v fc-match > /dev/null; then
        reelle="$(fc-match -f '%{family}' "$demandee:weight=80:slant=0" | cut -d, -f1)"
        if [ "$employee" != "$reelle" ]; then
            echo "ÉCHEC : l'avertissement annonce « $employee », fc-match rend « $reelle »." >&2
            exit 1
        fi
    fi
done

echo "B5 : polices présentes muettes, polices absentes nommées avec leur remplaçante."
