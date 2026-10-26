#!/usr/bin/env python3
"""Le contrôle des aides de geste du banc (poste N1).

Les tests d'interface synthétisent leurs gestes par les aides de MyPublisher.Tests.Interface/Rendu.cs,
région « Les gestes », et par elles seules : une aide recopiée de classe en classe finit par
oublier ce que l'original vérifiait (l'attente d'image du clic, le bouton tenu du glissé, l'échec
quand PageRoot manque). Ce script dit si une copie est revenue.

Ce qu'est une aide de geste, ici :

  - un membre ou une fonction locale qui n'est pas un test — sans [AvaloniaFact],
    [AvaloniaTheory], [Fact] ni [Theory] — et dont le corps appelle lui-même une méthode
    d'entrée d'Avalonia.Headless : MouseDown, MouseUp, MouseMove, MouseWheel, KeyPress,
    KeyPressQwerty, KeyRelease, KeyReleaseQwerty, KeyTextInput, DragDrop ;
  - ou un membre, ou une fonction locale, qui porte le nom d'une aide publique de la région
    « Les gestes » de Rendu.cs : un homonyme, même innocent, est une copie qui attend son heure,
    et un appel sans « Rendu. » ne dirait plus laquelle il vise.

Ce qui n'en est pas une : un test qui joue ses entrées en ligne, quand c'est ce qu'il éprouve ;
une aide d'une classe qui compose celles de Rendu sans rien synthétiser elle-même (« Tracer »,
« TirerLaPoignee ») ; un appel de méthode. Les commentaires et les littéraux sont blanchis avant
la lecture : un nom cité dans une phrase ne compte pas.

Dans Rendu.cs même, aucune entrée n'est synthétisée hors de la région.

Sortie : 0 si aucune aide n'est définie hors de la région, 1 sinon, en nommant chacune
(fichier:ligne). 2 si la région est introuvable ou presque vide — un contrôle qui ne trouverait
plus ses noms passerait sur tout.

    python3 Scripts/aides-de-geste.py [racine du dépôt]
    python3 Scripts/aides-de-geste.py --noms [racine du dépôt]   # les aides de la région, une par ligne
"""
import pathlib
import re
import sys

ENTREES = re.compile(r"\.(MouseDown|MouseUp|MouseMove|MouseWheel|KeyPress|KeyPressQwerty|KeyRelease|"
                     r"KeyReleaseQwerty|KeyTextInput|DragDrop)\s*\(")
TESTS = re.compile(r"\[\s*(?:Xunit\.)?(AvaloniaFact|AvaloniaTheory|Fact|Theory)\b")
REGION = re.compile(r"#region\s+Les gestes\b(?P<corps>.*?)#endregion", re.S)
PUBLIQUE = re.compile(r"\bpublic\s+static\s+[^=;{}()]*?\b(?P<nom>[A-Z]\w*)\s*(?:<[^<>()]*>)?\s*\(")
# Le moins d'aides que la région doit compter : en dessous, on lit mal le fichier.
MINIMUM = 10


def blanchir(texte):
    """Le texte, commentaires et littéraux remplacés par des blancs (les sauts de ligne gardés)."""
    sortie = list(texte)
    n = len(texte)

    def blanc(a, b):
        for k in range(a, b):
            if sortie[k] != "\n":
                sortie[k] = " "

    def caractere(i):
        j = i + 1
        j += 2 if texte[j] == "\\" else 1
        while texte[j] != "'":
            j += 1
        return j + 1

    def chaine(i):
        """Fin (exclue) du littéral qui commence en i."""
        j = i
        interpole = verbatim = False
        while texte[j] in "$@":
            interpole |= texte[j] == "$"
            verbatim |= texte[j] == "@"
            j += 1
        if texte.startswith('"""', j):
            ouvre = j
            while j < n and texte[j] == '"':
                j += 1
            guillemets = j - ouvre
            return texte.index('"' * guillemets, j) + guillemets
        j += 1
        while j < n:
            c = texte[j]
            if verbatim and texte.startswith('""', j):
                j += 2
                continue
            if not verbatim and c == "\\":
                j += 2
                continue
            if c == '"':
                return j + 1
            if interpole and c == "{":
                if texte.startswith("{{", j):
                    j += 2
                    continue
                profondeur = 1
                j += 1
                while profondeur:
                    d = texte[j]
                    if d == '"' or (d in "$@" and texte[j + 1:j + 2] in ('"', "$", "@")):
                        j = chaine(j)
                        continue
                    if d == "'":
                        j = caractere(j)
                        continue
                    profondeur += {"{": 1, "}": -1}.get(d, 0)
                    j += 1
                continue
            j += 1
        raise ValueError("chaîne non fermée")

    i = 0
    while i < n:
        c = texte[i]
        if texte.startswith("//", i):
            j = texte.find("\n", i)
            j = n if j < 0 else j
            blanc(i, j)
            i = j
        elif texte.startswith("/*", i):
            j = texte.index("*/", i) + 2
            blanc(i, j)
            i = j
        elif c == '"' or (c in "$@" and re.match(r'[$@]{1,3}"', texte[i:i + 4])):
            j = chaine(i)
            blanc(i, j)
            i = j
        elif c == "'":
            j = caractere(i)
            blanc(i, j)
            i = j
        else:
            i += 1
    return "".join(sortie)


MOTS_CLES = {"new", "return", "throw", "await", "else", "if", "while", "for", "foreach", "switch", "case",
             "using", "lock", "typeof", "nameof", "sizeof", "default", "checked", "unchecked", "when", "in",
             "is", "as", "out", "ref", "yield", "goto", "class", "struct", "record", "interface", "enum",
             "namespace", "catch", "fixed", "stackalloc", "base", "this", "var", "get", "set", "init",
             "operator", "implicit", "explicit", "delegate", "event", "not", "and", "or"}

# « type nom(… » ou « type nom<T>(… » : le type peut être générique, nullable, un tableau ou un tuple.
DECLARATION = re.compile(
    r"(?<![\w.])(?P<type>[A-Za-z_][\w.]*(?:<[^;{}=]*?>)?(?:\[\])*\??|\([^()]*\))\s+"
    r"(?P<nom>[A-Za-z_]\w*)\s*(?:<[^<>;{}()]*>)?\s*\(")


def fermer(code, i, ouvre, ferme):
    """L'indice qui suit le délimiteur fermant de celui ouvert en i."""
    profondeur = 0
    for j in range(i, len(code)):
        if code[j] == ouvre:
            profondeur += 1
        elif code[j] == ferme:
            profondeur -= 1
            if profondeur == 0:
                return j + 1
    raise ValueError("non fermé")


def declarations(code):
    """(nom, début, fin, est un test) de chaque méthode et fonction locale du code blanchi : une
    signature suivie d'un corps entre accolades ou d'une flèche. Un appel n'en a pas."""
    trouvees = []
    for m in DECLARATION.finditer(code):
        if m.group("type") in MOTS_CLES or m.group("nom") in MOTS_CLES:
            continue
        apres = fermer(code, m.end() - 1, "(", ")")
        suite = re.match(r"\s*(?:where\s[^{;]*?)?(\{|=>)", code[apres:])
        if not suite:
            continue
        debut = apres + suite.start(1)
        if suite.group(1) == "{":
            fin = fermer(code, debut, "{", "}")
        else:
            j, profondeur = debut, 0
            while not (code[j] == ";" and profondeur == 0):
                if code[j] in "({[":
                    profondeur += 1
                elif code[j] in ")}]":
                    profondeur -= 1
                j += 1
            fin = j + 1
        # Les attributs qui la précèdent : jusqu'au membre ou à l'instruction d'avant.
        avant = code[:m.start()]
        borne = max(avant.rfind(";"), avant.rfind("}"), avant.rfind("{"))
        trouvees.append((m.group("nom"), m.start(), fin, TESTS.search(avant[borne + 1:]) is not None))
    return trouvees


def ligne(texte, indice):
    return texte.count("\n", 0, indice) + 1


def noms_du_banc(rendu):
    """Les aides publiques de la région « Les gestes » de Rendu.cs, et l'étendue de la région."""
    code = blanchir(rendu)
    # La région se lit sur le texte, ses directives n'étant ni des commentaires ni des littéraux.
    region = REGION.search(rendu)
    if region is None:
        return None, None
    noms = {m.group("nom") for m in PUBLIQUE.finditer(code[region.start("corps"):region.end("corps")])}
    return noms, (region.start(), region.end())


def controler(racine):
    dossier = racine / "MyPublisher.Tests.Interface"
    rendu = (dossier / "Rendu.cs").read_text(encoding="utf-8")
    noms, region = noms_du_banc(rendu)
    if noms is None or len(noms) < MINIMUM:
        print(f"ÉCHEC : la région « Les gestes » de Rendu.cs est introuvable ou ne compte que "
              f"{0 if noms is None else len(noms)} aide(s) ({MINIMUM} au moins) : le contrôle ne lirait rien.")
        return 2

    fautes = []
    membres = 0
    for fichier in sorted(dossier.glob("*.cs")):
        texte = fichier.read_text(encoding="utf-8")
        code = blanchir(texte)
        if fichier.name == "Rendu.cs":
            for m in ENTREES.finditer(code):
                if not region[0] <= m.start() < region[1]:
                    fautes.append(f"{fichier.name}:{ligne(texte, m.start())}: « {m.group(1)} » hors de la région des gestes")
            continue
        for nom, debut, fin, test in declarations(code):
            membres += 1
            if test:
                continue
            entrees = sorted({m.group(1) for m in ENTREES.finditer(code, debut, fin)})
            raisons = []
            if entrees:
                raisons.append("synthétise " + ", ".join(entrees))
            if nom in noms:
                raisons.append("porte le nom d'une aide du banc")
            if raisons:
                fautes.append(f"{fichier.name}:{ligne(texte, debut)}: {nom} — {' ; '.join(raisons)}")

    for faute in fautes:
        print(faute)
    print(f"{len(noms)} aides de geste dans Rendu.cs ({', '.join(sorted(noms))}) ; "
          f"{membres} membres lus ailleurs, {len(fautes)} aide(s) hors du banc.")
    return 1 if fautes else 0


if __name__ == "__main__":
    arguments = sys.argv[1:]
    noms_seuls = bool(arguments) and arguments[0] == "--noms"
    if noms_seuls:
        arguments = arguments[1:]
    depot = pathlib.Path(arguments[0] if arguments else ".").resolve()
    if noms_seuls:
        trouves, _ = noms_du_banc((depot / "MyPublisher.Tests.Interface" / "Rendu.cs").read_text(encoding="utf-8"))
        print("\n".join(sorted(trouves or [])))
        sys.exit(0 if trouves else 2)
    sys.exit(controler(depot))
