"""Abnahme D6: B6 steht in Block B, Block C hat genau eine Ueberschrift."""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _todo import abschnitt, lies, melde, struktur  # noqa: E402

lines = lies()
fehler = []


def zeilen_mit(muster: str) -> list[int]:
    return [i for i, line in enumerate(lines) if re.match(muster, line)]


block_c = zeilen_mit(r"# Block C\b")
if len(block_c) != 1:
    fehler.append(f"'# Block C' steht {len(block_c)}x da statt 1x")
elif "Spec" not in lines[block_c[0]]:
    fehler.append("die ausfuehrliche Block-C-Ueberschrift (Spec-Reihenfolge) ist weg")

b5, b6, c1 = zeilen_mit(r"### B5 "), zeilen_mit(r"### B6 "), zeilen_mit(r"### C1 ")
if block_c and b5 and b6 and c1:
    if not b5[0] < b6[0] < block_c[0] < c1[0]:
        fehler.append(f"Reihenfolge falsch: B5 Z.{b5[0] + 1}, B6 Z.{b6[0] + 1}, "
                      f"Block C Z.{block_c[0] + 1}, C1 Z.{c1[0] + 1}")
    if "---" not in [l.strip() for l in lines[b6[0]:block_c[0]]]:
        fehler.append("zwischen B6 und Block C fehlt die Trennlinie ---")

einleitung = zeilen_mit(r"Die Spec priorisiert nach Spielgef")
if len(einleitung) != 1:
    fehler.append(f"Einleitung von Block C steht {len(einleitung)}x da statt 1x")
elif block_c and c1 and not block_c[0] < einleitung[0] < c1[0]:
    fehler.append("Einleitung von Block C steht nicht zwischen Ueberschrift und C1")

b6_text = "\n".join(abschnitt(lines, r"### B6 "))
for pflicht in ("Buildings.cs", "BuildingEntity", "MapGrid.AddBuilding"):
    if pflicht not in b6_text:
        fehler.append(f"B6-Inhalt unvollstaendig: {pflicht} fehlt")

fehler += struktur(lines, min_erledigt=52, min_offen=35, min_zeilen=420)
melde(fehler, "D6 erfuellt: Gliederung repariert")
