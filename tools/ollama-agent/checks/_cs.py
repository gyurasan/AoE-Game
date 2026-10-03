"""Kleine Hilfen, um C#-Quelltext in Abnahmeskripten gezielt zu pruefen."""

import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

SCREEN = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Screens/RTSGameplayScreen.cs")


def lies(pfad: Path = SCREEN) -> str:
    return pfad.read_text(encoding="utf-8-sig")


def methode(text: str, name: str) -> str | None:
    """Rumpf der ersten Methodendefinition 'name', oder None.

    Die Parameterliste wird per Klammerzaehlung abgeglichen, nicht per Regex:
    Tupel-Parameter wie 'List<(int x, int y)>' enthalten selbst Klammern - an
    genau denen scheiterte die erste Fassung und verwarf korrekte Arbeit.
    Aufrufe (danach ';', '.', ...) und 'new Name(...) { ... }' werden
    uebersprungen. Ein Ausdrucksrumpf ('=> ...;') kommt bis zum Semikolon zurueck.
    Klammern in Strings und Kommentaren werden nicht gesondert behandelt -
    fuer den Code dieses Projekts reicht das.
    """
    for treffer in re.finditer(rf"\b{name}\s*\(", text):
        davor = text[:treffer.start()].rstrip()
        if davor.endswith("new") or davor.endswith("."):
            continue
        i, tiefe = treffer.end(), 1
        while i < len(text) and tiefe:
            if text[i] == "(":
                tiefe += 1
            elif text[i] == ")":
                tiefe -= 1
            i += 1

        ausdruck = re.match(r"\s*=>", text[i:])
        if ausdruck:
            ende = text.find(";", i)
            return text[i:ende + 1] if ende >= 0 else None

        block = re.match(r"\s*\{", text[i:])
        if not block:
            continue
        start = i + block.end() - 1
        tiefe = 0
        for j in range(start, len(text)):
            if text[j] == "{":
                tiefe += 1
            elif text[j] == "}":
                tiefe -= 1
                if tiefe == 0:
                    return text[start:j + 1]
        return None
    return None


def melde(fehler: list[str], ok_text: str) -> None:
    if fehler:
        print("NICHT ERFUELLT:")
        for eintrag in fehler:
            print("  -", eintrag)
        raise SystemExit(1)
    print(ok_text)
