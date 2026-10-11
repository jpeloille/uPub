#!/usr/bin/env bash
#
# Recette du poste Q12 — aucune dépendance vulnérable connue.
#
#   Scripts/verifier-dependances.sh
#
# Elle se lance À PART, et non par « Scripts/verifier.sh » : elle lit la base des
# vulnérabilités sur nuget.org, donc demande le réseau, et peut tomber un jour sans
# qu'une ligne du dépôt ait changé. Le propriétaire l'a sortie de la vérification le
# 11 octobre 2026. À jouer avant une livraison, et quand on touche aux paquets.
# Le juge ne l'éprouve pas : sa contre-épreuve (point 3) le fait à chaque passage.
#
# Ce qu'elle éprouve :
#
#   1. la base des vulnérabilités NuGet a bien été consultée : la commande a abouti,
#      sa sortie est un relevé qui porte tous les projets de la solution, et elle ne
#      signale aucun problème. Sans réseau, la recette ÉCHOUE en le disant — elle ne
#      conclut jamais « rien à signaler » d'une base qu'elle n'a pas lue ;
#   2. sur tous les projets de la solution, paquets directs ET transitifs, aucun paquet
#      n'est affecté d'une vulnérabilité connue ;
#   3. contre-épreuve — un projet jetable qui tire Tmds.DBus.Protocol 0.21.2, la version
#      fautive, y est bien signalé, avancée GHSA-xrw6-gwf8-vvr9 comprise. C'est ce qui
#      prouve que le silence du point 2 vient de la base, et non d'une commande qui ne
#      sait plus rien signaler — un format de sortie qui change, une source qui
#      n'expose plus ses avancées, un cache vide qu'on prendrait pour une base vide ;
#   4. l'application compilée embarque bien ce qui vient d'être relu : son relevé de
#      dépendances (« MyPublisher.deps.json ») porte, paquet par paquet, les versions
#      que la restauration a résolues — Tmds.DBus.Protocol 0.21.3 et non plus 0.21.2.
#      Ce qui part chez l'utilisateur est ce qui compte.
#
# Pourquoi le relevé JSON (« --format json ») et non la sortie en clair : la sortie en
# clair dit « has no vulnerable packages » aussi bien quand la base est vide que quand
# elle est saine, et il faudrait lire une phrase pour distinguer un échec. Le JSON
# porte la liste des sources consultées et celle des problèmes rencontrés.
#
# Ce que la recette ne prétend pas : la base change sans nous. Une avancée publiée
# demain sur un paquet d'aujourd'hui fera tomber cette recette sans qu'une ligne du
# dépôt ait bougé. C'est voulu : c'est exactement ce qu'on veut apprendre.
#
# Hors réseau, NuGet sert un temps ce qu'il a gardé dans son cache HTTP : la recette
# passe alors sur une base relue récemment, ce qui reste une vraie lecture. Cache vide
# ou expiré, elle échoue.
#
set -euo pipefail
export LC_ALL=C
# Les messages de l'outil restent en anglais, où qu'on la joue : c'est eux qu'on
# reconnaît pour dire « pas de réseau ».
export DOTNET_CLI_UI_LANGUAGE=en
export DOTNET_NOLOGO=1

racine="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$racine"

travail="$(mktemp -d)"
trap 'rm -rf "$travail"' EXIT

# $1 projet ou solution · $2 fichier du relevé JSON
# Lit la base. Échoue explicitement si la commande n'aboutit pas : c'est presque
# toujours l'absence de réseau, et on le dit plutôt que de laisser croire à un vert.
relever() {
    if ! dotnet list "$1" package --vulnerable --include-transitive --format json \
            > "$2" 2> "$travail/erreur.txt"; then
        echo "ÉCHEC : la base des vulnérabilités NuGet n'a pas pu être lue pour $1." >&2
        if grep -qiE 'service index|Connection refused|Name or service not known|No such host|timed out|NU1301' \
                "$2" "$travail/erreur.txt"; then
            echo "Les sources NuGet sont injoignables : pas de réseau ? Cette recette ne" >&2
            echo "conclut pas sans la base, elle ne se joue pas hors ligne." >&2
        fi
        { head -20 "$2"; head -20 "$travail/erreur.txt"; } >&2
        exit 1
    fi
}

# $1 relevé JSON · $2 nom de l'étape, pour les messages
# Écrit une ligne par paquet vulnérable : « projet  paquet  version  cadre  avancées ».
# Sort en erreur si le relevé n'est pas un relevé fiable.
lire_releve() {
    python3 - "$1" "$2" <<'PY'
import json, sys

chemin, etape = sys.argv[1], sys.argv[2]
try:
    releve = json.load(open(chemin))
except ValueError:
    print(f"ÉCHEC ({etape}) : la sortie n'est pas un relevé JSON.", file=sys.stderr)
    print(open(chemin).read()[:800], file=sys.stderr)
    sys.exit(1)

# Un problème, même au niveau « warning », peut vouloir dire qu'une source n'a pas
# livré ses avancées (NU1900) : la conclusion ne vaudrait rien. On ne trie pas.
problemes = releve.get("problems", [])
if problemes:
    print(f"ÉCHEC ({etape}) : la commande signale des problèmes, le relevé n'est pas fiable.",
          file=sys.stderr)
    for p in problemes:
        print(f"    [{p.get('level')}] {p.get('project', '')} : {p.get('text', '')[:300]}",
              file=sys.stderr)
    sys.exit(1)

if not releve.get("sources"):
    print(f"ÉCHEC ({etape}) : aucune source NuGet consultée.", file=sys.stderr)
    sys.exit(1)

for projet in releve.get("projects", []):
    nom = projet["path"].rsplit("/", 1)[-1]
    for cadre in projet.get("frameworks", []):
        for genre in ("topLevelPackages", "transitivePackages"):
            for paquet in cadre.get(genre, []):
                avancees = [v.get("advisoryurl", "?") for v in paquet.get("vulnerabilities", [])]
                if avancees:
                    print(f"{nom}  {paquet['id']}  {paquet['resolvedVersion']}  "
                          f"{cadre['framework']}  {' '.join(avancees)}")
PY
}

# ------------------------------------------------------------------------------- 1

relever MyPublisher.sln "$travail/solution.json"
vulnerables="$(lire_releve "$travail/solution.json" solution)"

# Chaque projet de la solution doit figurer au relevé : un projet que la commande
# aurait sauté serait un projet dont le silence ne veut rien dire.
attendus="$(dotnet sln MyPublisher.sln list | grep -c '\.csproj$' || true)"
releves="$(python3 -c 'import json,sys; print(len(json.load(open(sys.argv[1])).get("projects", [])))' \
    "$travail/solution.json")"
if [ "$attendus" = 0 ] || [ "$releves" != "$attendus" ]; then
    echo "ÉCHEC : le relevé porte $releves projet(s), la solution en compte $attendus." >&2
    exit 1
fi

echo "  1. la base des vulnérabilités a été lue, pour les $attendus projets de la solution."

# ------------------------------------------------------------------------------- 2

if [ -n "$vulnerables" ]; then
    echo "ÉCHEC : la solution tire des paquets affectés d'une vulnérabilité connue :" >&2
    echo "$vulnerables" | sed 's/^/    /' >&2
    exit 1
fi

echo "  2. aucun paquet vulnérable, direct ou transitif."

# ------------------------------------------------------------------------------- 3

# Un projet jetable, hors du dépôt, qui ne fait que tirer la version fautive. Il hérite
# de la configuration NuGet de l'utilisateur ; si le dépôt en porte une à lui, on la lui
# donne aussi, pour que la contre-épreuve lise exactement les mêmes sources.
mkdir -p "$travail/temoin"
for config in nuget.config NuGet.config NuGet.Config; do
    if [ -f "$config" ]; then cp "$config" "$travail/temoin/"; fi
done
cat > "$travail/temoin/Temoin.csproj" <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Tmds.DBus.Protocol" Version="0.21.2"/>
    </ItemGroup>
</Project>
XML

relever "$travail/temoin/Temoin.csproj" "$travail/temoin.json"
temoin="$(lire_releve "$travail/temoin.json" contre-épreuve)"

if ! grep -q 'Tmds.DBus.Protocol  0.21.2  .*GHSA-xrw6-gwf8-vvr9' <<< "$temoin"; then
    echo "ÉCHEC : la version fautive n'est pas signalée sur le projet témoin." >&2
    echo "Le silence du point 2 ne prouve donc rien. Relevé du témoin :" >&2
    sed 's/^/    /' "$travail/temoin.json" >&2
    exit 1
fi

echo "  3. contre-épreuve : la 0.21.2 est bien signalée sur un projet témoin."

# ------------------------------------------------------------------------------- 4

# Les points 1 à 3 jugent le graphe que la restauration vient de résoudre, pas le
# binaire : une compilation antérieure à la correction embarquerait encore l'ancienne
# version. On compare donc, paquet par paquet, ce que la compilation a copié
# (« MyPublisher.deps.json ») à ce que la restauration a résolu (« project.assets.json »).
# Aucune version n'est écrite en dur : le jour où Avalonia tirera de lui-même une
# version corrigée et que la référence directe partira, ce point n'aura rien à changer.
# Jouée à part, elle compile elle-même : un binaire d'avant la correction ferait mentir
# ce point.
if ! dotnet build MyPublisher/MyPublisher.csproj -c Debug --nologo -v q > /dev/null 2>&1; then
    echo "ÉCHEC : la compilation ne passe pas." >&2
    exit 1
fi
deps="MyPublisher/bin/Debug/net10.0/MyPublisher.deps.json"

python3 - "$deps" MyPublisher/obj/project.assets.json <<'PY'
import json, sys

def paquets(chemin):
    return {cle.split("/", 1)[0]: cle.split("/", 1)[1]
            for cle, valeur in json.load(open(chemin))["libraries"].items()
            if valeur.get("type") == "package"}

compile, resolu = paquets(sys.argv[1]), paquets(sys.argv[2])
ecarts = sorted(nom for nom in compile if resolu.get(nom) != compile[nom])
if ecarts or "Tmds.DBus.Protocol" not in compile:
    print("ÉCHEC : l'application compilée n'embarque pas ce que la restauration a résolu :",
          file=sys.stderr)
    for nom in ecarts:
        print(f"    {nom} : compilé {compile[nom]}, résolu {resolu.get(nom, 'absent')}",
              file=sys.stderr)
    print("La compilation est-elle à jour ?", file=sys.stderr)
    sys.exit(1)

print(f"  4. l'application compilée embarque les {len(compile)} paquets relus, "
      f"dont Tmds.DBus.Protocol {compile['Tmds.DBus.Protocol']}.")
PY

echo "Q12 : aucune dépendance vulnérable connue, et le témoin prouve que la base répond."
