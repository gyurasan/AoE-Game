"""Abnahme D16: TODO.md haelt fest, was der Screenshot vom 2026-10-01 bestaetigt.

Momentaufnahme: eine spaetere Sichtpruefung darf die Saetze zu Recht aendern.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _todo import abschnitt, lies, melde, struktur  # noqa: E402

lines = lies()
fehler = []
wieder = "\n".join(abschnitt(lines, r"## Wiederaufnahme"))

if "225150" not in wieder:
    fehler.append("Wiederaufnahme: der Screenshot 225150 fehlt")
# Welcher Schritt, ist gleich: der Nutzer hat am 2026-10-07 drei Punkte davorgesetzt
schritt = re.search(r"^\d+\.\s+\*\*Sichtprüfung im Spiel\*\*[\s\S]*?(?=^\d+\.\s|\Z)", wieder, re.MULTILINE)
if not schritt:
    fehler.append("Wiederaufnahme: Schritt 'Sichtprüfung im Spiel' fehlt")
else:
    text = schritt.group(0)
    for offen in ("Bauen", "Q", "Fischen", "Kantenscrollen"):
        if offen not in text:
            fehler.append(f"Sichtprüfung: offener Punkt {offen} fehlt")
    # Bestaetigtes darf nicht mehr als ungeprueft dastehen
    if re.search(r"weiterhin\s+Wald", text):
        fehler.append("Sichtprüfung: Wald steht noch als ungeprüft da")
    # Gross oder klein: D17 beginnt den Satz mit "Bestätigt sind ..."
    if not re.search(r"bestätigt", text, re.IGNORECASE):
        fehler.append("Sichtprüfung: nennt nicht, was bestätigt ist")

c1 = "\n".join(abschnitt(lines, r"### C1 "))
if not re.search(r"^\s*- \[[ x]\][^\n]*Sichtprüfung[\s\S]{0,200}Fischen", c1, re.MULTILINE):
    fehler.append("C1: der Punkt zur Sichtprüfung nennt nicht, dass Fischen noch fehlt")

fehler += struktur(lines, min_erledigt=101, min_offen=18, min_zeilen=620)
melde(fehler, "D16 erfuellt: Befund des Screenshots vom 2026-10-01 festgehalten")
