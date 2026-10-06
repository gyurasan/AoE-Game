# Neue Gebäude - Stand 2026-10-06, hier weitermachen

Ziel: alle Gebäude aus `src/AoE.Core/Entities/Buildings.cs` baubar machen, mit Bild je
Zeitalter und einer zweiten Reihe Bautasten unter den bisherigen.

## Fertig

- **Bilder** (Qwen-Image, `tools/bilder/bilder.json`, Gruppen `gebaeude2_dunkel` bis
  `gebaeude2_imperial`): Kaserne, Palisadenmauer, Schießstand, Stall, Schmiede, Markt,
  Steinmauer, Belagerungswerkstatt, Universität, Kloster, Burg, Wunder - je Zeitalter ab
  dem, das sie freischaltet (`AgeRules.RequiredAgeOf`), 32 Bilder in blau und rot unter
  `Content/Gebaeude/<zeitalter>/`. Stil etwas realistischer als die bisherigen Gebäude.
  Mauern sind 1×1-Blöcke, die sich in jede Richtung aneinanderreihen.
- **Symbole** für 13 Bautasten unter `Content/Icons/` (inkl. Stadtzentrum).
- Alles in `AgeOfEvolutions.mgcb` eingetragen; das Spiel lädt sie noch nicht.
- `qwen_image.py`: ein Eintrag kann den Gruppenstil mit `"stil"` ersetzen (gebraucht, um
  der Steinmauer die Tür wegzumalen).

## Offen - Reihenfolge

Vorher: Spiel nicht gestartet (sperrt die Exe), ComfyUI aus (Ollama braucht ~27,5 GB).

1. `py tools/ollama-agent/vorbereitet/neubauten/vertrag.py arbeitsbaum` - spielt die
   Verträge ein: XML-Kommentare in `BuildingRules` (Kosten laut Spezifikation, Bauzeit und
   Größe nach AoE II), Rumpf `BuildingEntity.Create` mit `NotImplementedException`, neue
   Tests in `ConstructionTests.cs`, Ablaufgruppe `neubauten` in `tools/spielablauf`.
   Danach sind Tests und `neubauten` rot, bis L1 und L2 durch sind.
2. `py tools/ollama-agent/vorbereitet/neubauten/tasks_l.py` - trägt L1 und L2 in
   `tasks.json` ein (Fahnentücher aus `fahnen.json`, am Bild gemessen).
3. `cd tools/ollama-agent` und `py run_tasks.py --tasks L1 L2`.
   - L1: Kernregeln und `BuildingEntity.Create` (Abnahme: Build, Tests).
   - L2: `CoreBuildings`, `BuildMenu` mit Reihe 0/1, zweite Tastenreihe in `LayoutButtons`,
     Symbole, Bilder, Fahnentücher, Mauer-Setzmodus bleibt an (Abnahme: Build GL/DX, Tests,
     `spielablauf neubauten leiste turm ...`, `checks/l2_neubauten.py`,
     `checks/c6a_bewegt.py`, Startprobe GL/DX).
4. Doku-Aufgabe D24 noch schreiben (README Spielstand und Tasten, PROJEKT_STRUKTUR,
   TODO C13/Erledigt); Abnahme `checks/d24_doku.py` plus d5, d9, d15 (zählen selbst).
   d5 und d9 waren schon vorher rot (Testzahlen veraltet).
5. Foto im Spiel (Foto-Patch nur in einer Scratchpad-Kopie): zweite Tastenreihe, je
   Zeitalter ein paar Gebäude, Mauerreihe.

`ref.py` ist die Referenzumsetzung, gegen die alle Abnahmen am 2026-10-06 grün waren
(Build GL/DX, 215 Tests, alle Ablaufgruppen, c6a, l2_neubauten, Startprobe GL/DX). Es
schreibt nur in eine Kopie namens `spiegel/` im Scratchpad - den Ordner dafür dorthin
kopieren, nie in den Arbeitsbaum.

## Entscheidungen

- Kaserne ab der Dunklen Zeit wie im Original (AoE II) und wie `AgeRules` - TODO C13 sagt
  noch Feudalzeit.
- Tasten der zweiten Reihe: K Kaserne, P Palisadenmauer, S Schießstand, L Stall,
  E Schmiede, R Markt, W Steinmauer, Z Stadtzentrum, X Belagerungswerkstatt,
  U Universität, O Kloster, C Burg, N Wunder.
- Werte (Lebenspunkte, Sicht) der neuen Typen nach AoE II; die Gebäude stehen nur,
  Funktion (Einheiten, Forschung, Handel) fehlt noch.
- Übrige Hilfsskripte hier: `katalog_neu.py`/`icons_neu.py` haben die Einträge in
  `bilder.json` geschrieben, `mgcb_neu.py` die Inhaltsliste, `fahnen.py` die Fahnen
  vermessen - nur nötig, wenn Bilder neu erzeugt werden.
