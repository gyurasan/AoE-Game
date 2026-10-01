"""Abnahme D11: Doku nach Nebel (C7n), Maussteuerung (C8a) und Gebaeuden (B6).

Aufruf: py d11_doku.py a   (TODO.md)
        py d11_doku.py b   (docs/PROJEKT_STRUKTUR.md; Zeilen- und Testzahlen prueft d9)
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _todo import abschnitt, lies, melde, struktur, testzahlen  # noqa: E402

teil = sys.argv[1] if len(sys.argv) > 1 else ""
fehler = []

if teil == "a":
    lines = lies()
    text = "\n".join(lines)
    zahlen = testzahlen()
    n = sum(zahlen.values())

    def block(kopf: str) -> str:
        return "\n".join(abschnitt(lines, kopf))

    wieder = block(r"## Wiederaufnahme")
    if not re.search(rf"dotnet test[^\n]*\n?[^\n]*{n}/{n}", wieder):
        fehler.append(f"Wiederaufnahme: Testzeile nennt nicht die gezaehlten {n}/{n}")
    for pflicht in ("C7n", "C8a", "B6", "Linksklick", "Screenshots"):
        if pflicht not in wieder:
            fehler.append(f"Wiederaufnahme: {pflicht} fehlt")
    for alt in (r"\*\*Nebel \(C7\)\*\* — seit B5", r"\*\*B6 Gebäude\*\* auf"):
        if re.search(alt.replace(" ", r"\s+"), wieder):
            fehler.append(f"Wiederaufnahme: erledigter Schritt steht noch in der Liste: /{alt}/")

    zeile = next((l for l in lines if l.startswith("| `tests/AoE.Tests`")), "")
    if f"{n}/{n}" not in zeile or f"Nebel ({zahlen.get('FogOfWarTests.cs')})" not in zeile:
        fehler.append(f"Tabellenzeile tests/AoE.Tests: {n}/{n} und Nebel ({zahlen.get('FogOfWarTests.cs')}) erwartet")

    if re.search(r"Rechtsklick\s+auf\s+eine\s+Ressource", text):
        fehler.append("noch 'Rechtsklick auf eine Ressource' - seit C8a ist es der Linksklick")

    for kopf, name in ((r"### B5 ", "B5"), (r"### B6 ", "B6")):
        if re.search(r"^\s*- \[ \]", block(kopf), re.MULTILINE):
            fehler.append(f"{name}: es ist noch ein Punkt offen")
    b6_kopf = next((l for l in lines if l.startswith("### B6 ")), "")
    if "erledigt" not in b6_kopf:
        fehler.append("B6: Ueberschrift nicht als erledigt markiert")
    if "CoreBuildings" not in block(r"### B6 "):
        fehler.append("B6: CoreBuildings wird nicht genannt")
    if not re.search(r"^\s*- \[x\][^\n]*Nebel", block(r"### C7 "), re.MULTILINE):
        fehler.append("C7: erledigter Punkt zum Nebel fehlt")
    c8 = block(r"### C8 ")
    for pflicht in ("C8a", "Linksklick", "rechten Taste"):
        if pflicht not in c8:
            fehler.append(f"C8: {pflicht} fehlt")
    if re.search(r"Rechtsklick\s*→\s*Bewegung", c8):
        fehler.append("C8: 'Rechtsklick → Bewegung' ist ueberholt")
    if not re.search(r"^\s*- \[x\][^\n]*ziehen", block(r"### C10 "), re.MULTILINE):
        fehler.append("C10: erledigter Punkt zum Ziehen der Karte fehlt")
    erledigt = block(r"# Erledigt")
    if f"{n} Unit-Tests" not in erledigt or "C7n" not in erledigt:
        fehler.append(f"Erledigt-Liste: '{n} Unit-Tests' und C7n erwartet")

    fehler += struktur(lines, min_erledigt=70, min_offen=26, min_zeilen=480)
    melde(fehler, "D11a erfuellt: TODO.md kennt Nebel, Maussteuerung und B6")

elif teil == "b":
    text = Path("docs/PROJEKT_STRUKTUR.md").read_text(encoding="utf-8-sig")
    if re.search(r"MapGrid\s+ab\s+Zeile", text):
        fehler.append("MapGrid mit Zeilenangabe - die veraltet mit jeder Aenderung")
    anbindung = text.split("## Anbindung an AoE.Core", 1)[-1]
    aktiv, _, nie = anbindung.partition("Angebunden, aber nie aufgerufen")
    for pflicht in ("VisibilitySystem", "UpdateFogOfWarForPlayer", "Building.Core", "CoreBuildings"):
        if pflicht not in aktiv:
            fehler.append(f"Anbindung: '{pflicht}' fehlt bei dem, was zur Laufzeit aktiv ist")
    for weg in (r"\*\*Nebel:\*\*", r"\*\*Gebäude:\*\*"):
        if re.search(weg, nie):
            fehler.append(f"Anbindung: {weg} steht noch unter 'nie aufgerufen'")
    if "**Kampf:**" not in nie:
        fehler.append("Anbindung: der Punkt Kampf muss unter 'nie aufgerufen' bleiben")
    melde(fehler, "D11b erfuellt: PROJEKT_STRUKTUR.md kennt Nebel und Gebaeude")

else:
    sys.exit("Aufruf: py d11_doku.py a|b")
