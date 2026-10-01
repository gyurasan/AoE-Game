"""Gemeinsame Pruefbausteine fuer TODO.md (Abnahme von D5 und D6).

Die Pruefungen laufen mit der Repo-Wurzel als Arbeitsverzeichnis, so wie
run_tasks.py die verify-Kommandos startet.
"""

import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

# Jede Aufgabe muss nach einer Aenderung genau einmal als Ueberschrift da sein.
IDS = (
    [f"H{i}" for i in range(1, 8)]
    + [f"E{i}" for i in range(1, 5)]
    + [f"B{i}" for i in range(1, 7)]
    + [f"C{i}" for i in range(1, 11)]
    + [f"D{i}" for i in range(1, 5)]
)


def lies() -> list[str]:
    return Path("TODO.md").read_text(encoding="utf-8-sig").splitlines()


def testzahlen() -> dict[str, int]:
    """Gezaehlte Testfaelle je Testdatei - Doku-Pruefungen vergleichen damit,
    statt eine Zahl festzuschreiben, die jeder neue Test bricht.

    Gezaehlt wird wie 'dotnet test': jedes [Fact] ein Fall, jede [InlineData]-Zeile
    einer [Theory] ebenfalls. Die erste Fassung zaehlte [Theory] einmal und kam
    seit C1t/C2a auf 65, waehrend der Testlauf 72 meldete.
    """
    return {f.name: len(re.findall(r"\[(?:Fact|InlineData)\b", f.read_text(encoding="utf-8-sig")))
            for f in sorted(Path("tests/AoE.Tests").glob("*.cs"))}


def abschnitt(lines: list[str], kopf: str) -> list[str]:
    """Zeilen ab der Ueberschrift, die auf 'kopf' passt, bis vor die naechste Ueberschrift."""
    for start, line in enumerate(lines):
        if re.match(kopf, line):
            ende = start + 1
            while ende < len(lines) and not re.match(r"#{1,3} ", lines[ende]):
                ende += 1
            return lines[start:ende]
    return []


def struktur(lines: list[str], min_erledigt: int, min_offen: int, min_zeilen: int) -> list[str]:
    """Nichts verloren: alle Aufgaben-Ueberschriften einmal, Checkboxen und Umfang nicht geschrumpft.

    Geprueft wird die Gesamtzahl der Checkboxen, nicht die der offenen: wird
    ein Punkt erledigt, sinkt die Zahl der offenen zu Recht. Die erste Fassung
    verlangte eine Mindestzahl offener Punkte und schlug an, sobald spaetere
    Aufgaben Punkte abhakten.
    """
    fehler = []
    for task_id in IDS:
        anzahl = sum(1 for line in lines if re.match(rf"### {task_id} ", line))
        if anzahl != 1:
            fehler.append(f"Ueberschrift {task_id}: {anzahl}x statt 1x")
    erledigt = sum(1 for line in lines if re.match(r"\s*- \[x\]", line))
    offen = sum(1 for line in lines if re.match(r"\s*- \[ \]", line))
    if erledigt < min_erledigt:
        fehler.append(f"erledigte Checkboxen: {erledigt}, erwartet mindestens {min_erledigt}")
    if erledigt + offen < min_erledigt + min_offen:
        fehler.append(f"Checkboxen insgesamt: {erledigt + offen}, erwartet mindestens "
                      f"{min_erledigt + min_offen} - wurde etwas geloescht?")
    if len(lines) < min_zeilen:
        fehler.append(f"nur {len(lines)} Zeilen, erwartet mindestens {min_zeilen}")
    return fehler


def melde(fehler: list[str], ok_text: str) -> None:
    if fehler:
        print("NICHT ERFUELLT:")
        for eintrag in fehler:
            print("  -", eintrag)
        raise SystemExit(1)
    print(ok_text)
