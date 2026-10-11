#!/usr/bin/env bash
#
# Recette du poste Q17 — le texte composé suit une collection remplacée.
#
# « Spans » et « Paragraphs » avaient un accesseur public en écriture, et
# « ControleDeBloc » s'abonne à ces deux collections, pas à leur remplacement. Un code
# qui les aurait remplacées sur un bloc affiché aurait laissé le contrôle accroché à
# l'ancienne collection, et sourd à la nouvelle. Deux remèdes étaient ouverts : écouter
# le remplacement, ou fermer l'accesseur. Le poste a fermé — « init » : l'initialiseur
# et le sérialiseur peuvent encore les poser, plus personne après. PLAN.md dit pourquoi.
#
# La recette du plan — un contrôle affiché dont on remplace les fragments redessine au
# style de la nouvelle collection — ne peut donc plus se jouer : le geste ne compile
# plus. C'est cela qu'elle éprouve, en deux temps.
#
#   1. LE COMPILATEUR, interrogé pour de bon. Une sonde, compilée contre le noyau tel
#      qu'il vient d'être construit, affecte les trois collections qu'un contrôle de
#      bloc écoute — TextFrame.Spans, TextFrame.Paragraphs, TableCell.Spans — sur un
#      objet déjà construit : trois erreurs CS8852, une par ligne, sont exigées. Une
#      seconde sonde fait ce qui doit rester permis — poser par l'initialiseur, prêter
#      la collection d'une case, remplir et vider sur place — et doit compiler. Sans
#      elle, un noyau qui ne compilerait plus du tout passerait le premier temps.
#   2. LES TESTS du noyau marqués « poste=Q17 » : la marque « init » sur chaque
#      collection observable de TextFrame et de TableCell, découvertes par réflexion ;
#      la lecture d'un document, qui doit encore les poser ; la case qui prête toujours
#      SA collection au bloc qu'on construit pour elle. Leur compte est vérifié.
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

# Le noyau et sa seule dépendance, tels que les tests du noyau les emploient.
bin="$racine/MyPublisher.Tests/bin/Debug/net10.0"
for dll in MyPublisher.Core.dll CommunityToolkit.Mvvm.dll; do
    if [ ! -f "$bin/$dll" ]; then
        echo "ÉCHEC : $bin/$dll est introuvable. Compiler d'abord." >&2
        exit 1
    fi
done

# ----------------------------------------------------------------- 1. le compilateur

# $1 : le dossier de la sonde · $2 : son code. Sort avec le code du compilateur ; ses
# messages sont dans $1/sortie.txt.
sonder() {
    mkdir -p "$1"
    cat > "$1/Sonde.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="MyPublisher.Core"><HintPath>$bin/MyPublisher.Core.dll</HintPath></Reference>
    <Reference Include="CommunityToolkit.Mvvm"><HintPath>$bin/CommunityToolkit.Mvvm.dll</HintPath></Reference>
  </ItemGroup>
</Project>
XML
    printf '%s\n' "$2" > "$1/Sonde.cs"
    dotnet build "$1/Sonde.csproj" --nologo -v q -o "$1/sortie" > "$1/sortie.txt" 2>&1
}

interdite='using System.Collections.ObjectModel;
using MyPublisher.Models;

public static class Sonde
{
    public static void Remplacer(TextFrame bloc, TableCell cellule)
    {
        bloc.Spans = new ObservableCollection<StyleSpan>();
        bloc.Paragraphs = new ObservableCollection<ParagraphSpan>();
        cellule.Spans = new ObservableCollection<StyleSpan>();
    }
}'

permise='using MyPublisher.Models;

public static class Sonde
{
    public static TextFrame Construire(TableCell cellule)
    {
        var bloc = new TextFrame { Text = "abc", Spans = cellule.Spans, Paragraphs = [] };
        bloc.Spans.Clear();
        bloc.Spans.Add(new StyleSpan { Start = 0, Length = 1, Bold = true });
        bloc.Paragraphs.Add(new ParagraphSpan { Start = 0, Length = 1 });
        return bloc;
    }
}'

if sonder "$travail/interdite" "$interdite"; then
    echo "ÉCHEC : remplacer une collection écoutée compile encore." >&2
    echo "Un contrôle de bloc affiché resterait accroché à l'ancienne collection." >&2
    exit 1
fi
refus="$(grep -oE 'Sonde\.cs\([0-9]+,[0-9]+\): error CS8852' "$travail/interdite/sortie.txt" | sort -u | wc -l)"
autres="$(grep -E ': error ' "$travail/interdite/sortie.txt" | grep -v 'CS8852' | sort -u || true)"
if [ "$refus" != 3 ] || [ -n "$autres" ]; then
    echo "ÉCHEC : trois refus CS8852 attendus (un par collection), $refus obtenu(s)." >&2
    sort -u "$travail/interdite/sortie.txt" | grep -E 'error' | sed 's/^/    /' >&2
    exit 1
fi

if ! sonder "$travail/permise" "$permise"; then
    echo "ÉCHEC : ce qui doit rester permis ne compile plus :" >&2
    grep -E 'error' "$travail/permise/sortie.txt" | sort -u | sed 's/^/    /' >&2
    exit 1
fi

echo "  1. le compilateur refuse les trois remplacements (CS8852) ; initialiseur, prêt et remplissage sur place compilent."

# ------------------------------------------------------------------------ 2. les tests

attendus=6
if ! dotnet test MyPublisher.Tests/MyPublisher.Tests.csproj ${MYPUBLISHER_DEJA_COMPILE:+--no-build} \
        --nologo -v q --filter "poste=Q17" > "$travail/tests.txt" 2>&1; then
    sed 's/^/    /' "$travail/tests.txt" >&2
    echo "ÉCHEC : les tests du noyau de Q17 ne passent pas." >&2
    exit 1
fi
joues="$(sed -n 's/.*Passed: *\([0-9][0-9]*\).*/\1/p' "$travail/tests.txt" | head -1)"
if [ "${joues:-0}" -lt "$attendus" ]; then
    echo "ÉCHEC : $attendus tests attendus sous « poste=Q17 », ${joues:-0} joués." >&2
    echo "Un filtre qui ne désigne plus rien sort en 0 : la recette ne s'en contente pas." >&2
    exit 1
fi
echo "  2. $joues tests du noyau joués."

echo "Q17 : les collections qu'un contrôle de bloc écoute ne se remplacent plus après la construction."
