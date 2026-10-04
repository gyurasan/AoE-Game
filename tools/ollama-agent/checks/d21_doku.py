"""Abnahme D21: TODO, Projektstruktur und README kennen Farm und Tiere im neuen Stil (C7m, C7h, C7t).

Prüft Aussagen, keine Formulierungen; Leerzeichen stehen für beliebigen
Leerraum, weil die Doku über eingerückte Zeilen umbricht. Die Zeilenzahlen
misst d9_projekt_struktur.py.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import melde  # noqa: E402

todo = Path("TODO.md").read_text(encoding="utf-8-sig")
ps = Path("docs/PROJEKT_STRUKTUR.md").read_text(encoding="utf-8-sig")
readme = Path("README.md").read_text(encoding="utf-8-sig")
fehler = []


def hat(text, muster):
    return re.search(muster.replace(" ", r"\s+"), text) is not None


# TODO: Wiederaufnahme nicht mehr bei den uncommitteten Rehen
wieder = todo.split("## Wiederaufnahme", 1)[-1].split("Alles baut und läuft", 1)[0]
if hat(wieder, r"Rehe \(Wild\)\*\* — der Code steht uncommittet"):
    fehler.append("TODO: die Wiederaufnahme nennt noch die uncommitteten Rehe als letzten Stand")
for kennung in ("C7h", "C7t"):
    if not re.search(rf"- \[x\][^\n]*(\n\s+[^\n-][^\n]*)*\*\((Agent )?{kennung}\)\*", todo):
        fehler.append(f"TODO: im Block C7 fehlt der abgehakte Punkt für {kennung}")
for begriff in ("Felder/weizen", "Felder/acker", "WildAnimal", "herde"):
    if begriff not in todo:
        fehler.append(f"TODO: {begriff} wird nirgends genannt")
schritte = todo.split("Nächste sinnvolle Schritte", 1)[-1]
zwei = re.search(r"\n2\. \*\*Restliche Grafik im neuen Stil\*\*(.*?)\n3\. ", schritte, re.S)
if zwei and re.search(r"Schafe|Farm", zwei.group(1)):
    fehler.append("TODO, Schritt 2: Schafe oder Farm stehen noch als vom Code gezeichnet da")
sieben = re.search(r"\n7\. \*\*Wild(.*?)(\n\n|\n8\. )", schritte, re.S)
if sieben and re.search(r"Reh-Sprites\s+generieren", sieben.group(1)):
    fehler.append("TODO, Schritt 7: die Reh-Sprites stehen noch als offen da")

# Projektstruktur: Content-Ordner und Abschnitt Grafik
inhalt = next((l for l in ps.splitlines() if "Content/ -" in l), "")
for ordner in ("Felder/", "Tiere/"):
    if ordner not in inhalt:
        fehler.append(f"PROJEKT_STRUKTUR: Content-Zeile nennt {ordner} nicht")
grafik = ps.split("## Grafik", 1)[-1]
if not hat(grafik, r"Schafe und Rehe"):
    fehler.append("PROJEKT_STRUKTUR, Grafik: Schafe und Rehe aus Qwen-Image fehlen")
if "DrawAnimal" not in grafik:
    fehler.append("PROJEKT_STRUKTUR, Grafik: dass das Spiel den Schritt der Tiere rechnet (DrawAnimal), fehlt")

# README: Abschnitt Grafik
rgrafik = readme.split("## Grafik", 1)[-1].split("\n## ", 1)[0]
for begriff in ("Felder", "Schafe", "Rehe"):
    if begriff not in rgrafik:
        fehler.append(f"README, Grafik: {begriff} fehlt")

melde(fehler, "D21 erfuellt: TODO, Projektstruktur und README kennen Farm und Tiere im neuen Stil")
