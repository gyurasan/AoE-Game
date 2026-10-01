"""Abnahme C1c: untaetige Dorfbewohner werden gezaehlt, angezeigt und per Taste angesprungen."""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

text = lies()
fehler = []

leer = methode(text, "IdleVillagers")
if leer is None:
    fehler.append("Methode IdleVillagers fehlt")
else:
    for pflicht, name in ((r"CoreVillager", "Dorfbewohner-Pruefung"), (r"UnitState\.Idle", "Idle-Zustand"),
                          (r"Job\s*(==|is)\s*null", "kein Auftrag"), (r"OwnerId\s*==\s*0", "nur Spieler 0")):
        if not re.search(pflicht, leer):
            fehler.append(f"IdleVillagers prueft {name} nicht ({pflicht})")

eingabe = methode(text, "HandleRtsInput") or ""
if "Keys.OemPeriod" not in eingabe:
    fehler.append("HandleRtsInput reagiert nicht auf die Punkt-Taste")
if not re.search(r"previousKeyboard\.IsKeyUp\(\s*Keys\.OemPeriod\s*\)|!\s*previousKeyboard\.IsKeyDown\(\s*Keys\.OemPeriod\s*\)", eingabe):
    fehler.append("Punkt-Taste ohne Flankenerkennung - gehalten wuerde sie jeden Frame weiterschalten")
if not re.search(r"previousKeyboard\s*=\s*keyboard", eingabe):
    fehler.append("previousKeyboard wird nicht nachgefuehrt")
if not re.search(r"IdleVillagers\s*\(", eingabe):
    fehler.append("HandleRtsInput nutzt IdleVillagers nicht")

anzeige = methode(text, "DrawUI") or ""
for pflicht in ("Untätig", "IdleVillagers", "Color.Orange", "LP", "Traglast", "CARRY_CAPACITY"):
    if pflicht not in anzeige:
        fehler.append(f"DrawUI: {pflicht} fehlt")
if "bewegen/sammeln" not in text:
    fehler.append("Hilfetext nennt das Sammeln per Rechtsklick nicht")

namen = methode(text, "ResourceName") or ""
for wort in ("Nahrung", "Holz", "Gold", "Stein"):
    if wort not in namen:
        fehler.append(f"ResourceName: {wort} fehlt")

melde(fehler, "C1c erfuellt: Leerlauf sichtbar und per Taste erreichbar")
