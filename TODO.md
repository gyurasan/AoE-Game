# TODO — Age of Empires Clone

Offene Punkte aus dem Abgleich von `AgeOfEmpires.md`, `AgeOfEmpiresClone/game_plan.md`
und `README.md` gegen den tatsächlichen Code.

**Stand: 16.08.2026** — Erledigt am 16.08.: Kamera-Koordinatenbug, Zufälligkeit Kartengenerierung, ImageSharp 3.1.5 → 3.1.12 (behebt GHSA-2cmq-823j-5qj8 und GHSA-rxmq-m78w-7wmc; 4.x ist lizenzpflichtig, daher 3.1.x).

Reihenfolge = Abarbeitungsreihenfolge. Erst Block 0, sonst wird zweimal dasselbe gebaut.

---

## 0. Struktur — zuerst entscheiden

Es gibt zwei getrennte Codebasen. Die vollständigere davon läuft nie.

| | `src/AoE.Core/` (net10.0) | `AgeOfEmpiresClone/` (net9.0) |
|---|---|---|
| Inhalt | 2.547 Zeilen: Schadensformel mit Rüstungsklassen, echtes A*, Formationen, 3-Zustands-Sichtbarkeit, Gebäude + Technologien, Villager-Loop | das laufende MonoGame-Spiel |
| Tests | 15 Unit-Tests | keine |
| Im Spiel benutzt | **nein** | — |

`AgeOfEmpiresClone.sln` enthält nur `Core` + `DesktopGL`, keine Referenz auf `AoE.Core`.
Die Frameworks passen nicht zusammen (net10 vs. net9). Das Spiel hat alles in `Data/`
ein zweites Mal und schlechter implementiert.

- [ ] **Entscheidung treffen:** `AoE.Core` auf net9.0 ziehen, in die Solution aufnehmen
      und `Data/` dagegen ersetzen — **oder** `AoE.Core` samt Tests löschen und alles
      im MonoGame-Projekt neu aufbauen.
- [ ] Folgearbeit aus der Entscheidung umsetzen (Referenz + Ersetzung, bzw. Löschung).

---

## 1. Bugs, die Bestehendes kaputtmachen

- [x] **Auswahl und Bewegung ignorieren die Kamera.**
      `HandleRtsInput` übergibt rohe Mauskoordinaten an `WorldToGrid`
      (`AgeOfEmpiresClone.Core/Screens/RTSGameplayScreen.cs:150`); `ScreenToWorld`
      existiert (`:480`), wird aber nie benutzt. Die Kamera ist schon beim Start
      versetzt → Einzelklick und Rechtsklick treffen von Anfang an die falsche Kachel.
      Die *Rechteck*-Auswahl (`:174`) rechnet die Kamera dagegen ein — inkonsistent.
      **Fix 16.08.:** `mouseGridPos = WorldToGrid(ScreenToWorld(mausPos))` — jetzt
      konsistent mit der Rechteck-Auswahl.
- [x] **Kartengenerierung ist nicht zufällig.**
      `RandomInt` benutzt `DateTime.Now.Ticks % (max-min)` in einer engen Schleife
      (`Data/TileMap.cs:246`) → alle Ressourcenflecken landen praktisch übereinander.
      **Fix 16.08.:** eigener `Random`-Instanz pro Map, `_random.Next(min, max)`;
      zusätzlich Guard `max <= min` gegen `Next`-Exception.

---

## 2. Prioritätenliste aus `AgeOfEmpires.md` (Punkte 1–10)

### 1 — Dorfbewohner-Loop (nicht funktionsfähig)

- [ ] `HandleMouseClick` wird **nirgends aufgerufen** (`RTSGameplayScreen.cs:510`) —
      die einzige Stelle, die `UnitState.Gathering` setzt. Rechtsklick auf eine
      Ressource bewegt nur.
- [ ] `UnitState.Returning` wird gesetzt, in `UpdateUnits` aber nicht behandelt →
      Einheit bleibt hängen.
- [ ] `DeliverResource` schreibt unabhängig von der Position gut — kein Laufweg,
      keine Abgabestellen.
- [ ] Abgabestellen implementieren (Stadtzentrum, Mühle, Holzfällerlager, Bergbaulager)
      inkl. „nächste Abgabestelle gewinnt".
- [ ] `gatherTimer` ist ein einziger Zähler für *alle* Einheiten → pro Einheit führen.
- [ ] Automatische Fortsetzung nach Abgabe + automatische Nachbesetzung bei
      erschöpfter Quelle.
- [ ] Traglast 10 mit echten Sammelraten (~0,3–0,4 /s) statt Hochzählen alle 2 s.
- [ ] Erfundene „70 % Effizienz" bei der Abgabe entfernen (`RTSGameplayScreen.cs:341`) —
      gibt es in AoE nicht.

### 2 — Vier getrennte Ressourcen (halb)

- [ ] **Nahrung hat keine einzige Quelle auf der Karte.** `AddResourcePatches`
      erzeugt nur Wood/Stone/Gold (`Data/TileMap.cs:69-127`).
- [ ] Nahrungsquellen ergänzen: Schafe, Beerenbüsche, Wildschwein, Hirsch, Farmen, Fisch.
- [ ] Verderben von erlegtem Wild.

### 3 — Bevölkerungslimit (nur Anzeige)

- [ ] `PopulationLimit` ist hart auf 10 (`Data/Player.cs:22`).
- [ ] Häuser bauen → +5 Plätze; Stadtzentrum +5.
- [ ] Produktion bei erreichtem Limit blockieren.

### 4 — Zeitalter als Gate (fehlt)

- [ ] `AdvanceAge()` existiert, wird nie aufgerufen (`Data/Player.cs:148`).
- [ ] Kostenprüfung + Forschungszeit im Stadtzentrum.
- [ ] Gebäude und Einheiten pro Zeitalter freischalten.
- [ ] Kosten gegen die Spec korrigieren (Feudalzeit: 500 Nahrung, aktuell zusätzlich
      200 Holz; Ritter-/Imperialzeit ebenfalls abweichend).

### 5 — Schadensformel mit Angriffs-/Rüstungsklassen (fehlt im Spiel)

- [ ] `Unit` hat ein einzelnes `AttackPower` + `Armor` und einen pauschalen
      2×-Multiplikator (`Data/Unit.cs:231`) — genau das, was die Spec als
      unzureichend beschreibt. Ersetzen durch Listen von Angriffs- und Rüstungsklassen.
- [ ] **Es gibt überhaupt keinen Kampf:** `UnitState.Attacking` wird nie gesetzt,
      kein Angriff, kein Sterben.
- [ ] Korrekte Formel liegt in `src/AoE.Core/Combat/DamageCalculator.cs` — unbenutzt
      (siehe Block 0).

### 6 — Endliche Ressourcen (invertiert)

- [ ] `tile.ResourceAmount` wird beim Sammeln **nie verringert**.
- [ ] `UpdateResources` regeneriert stattdessen jeden Frame jede Kachel bis 100
      (`RTSGameplayScreen.cs:344`) → die Partie hat kein natürliches Ende. Entfernen.
- [ ] Nebenbefund: `(int)(dt * 0.1f)` ist immer 0 — die Regeneration ist faktisch
      toter Code.

### 7 — Isometrische Darstellung (fehlt komplett)

- [ ] `WorldToScreen` ist rein orthogonal (`RTSGameplayScreen.cs:473`).
- [ ] Rautenprojektion 2:1 einbauen (Formeln in `AgeOfEmpires.md`).
- [ ] Tiefensortierung nach `gridX + gridY`.
- [ ] Rückrechnung Maus → Gitter (nicht per Division).
- [ ] Braucht Kachel- und Einheiten-Sprites (siehe Block 4).

### 8 — Fog of War (nicht angeschlossen)

- [ ] `Data/FogOfWar.cs` ist vollständig, `TileMap` legt es an — aber
      `UpdateFogOfWarForPlayer` wird nie gerufen.
- [ ] Im Screen wird nichts davon gezeichnet.
- [ ] Zustand „erforscht, aber veraltet" (letzter bekannter Stand) existiert im
      Renderer gar nicht.
- [ ] Sichtweite pro Einheitentyp statt pauschal 6 (`Data/FogOfWar.cs:100`).

### 9 — A*-Wegfindung (fehlt)

- [ ] `TileMap.FindPath` interpoliert eine **gerade Linie** und prüft Begehbarkeit
      nur für die Zielkachel (`Data/TileMap.cs:214`) → Einheiten laufen durch Wasser
      und Gebäude.
- [ ] Gruppenbewegung, Formationen, gemeinsame Geschwindigkeit.
- [ ] Kollisionsvermeidung („bleibt nicht stecken" vor „optimaler Weg").
- [ ] Echtes A* + Formationen liegen in `src/AoE.Core/Pathfinding/` — unbenutzt
      (siehe Block 0).

### 10 — HUD (nur eine Textzeile)

- [ ] Ressourcenleiste oben mit Icons statt reinem Text.
- [ ] Kommandoleiste unten: Aktions-Icons zur aktuellen Auswahl.
- [ ] Auswahl-Info: Name, Portrait, HP, Angriff/Rüstung, Produktions-Warteschlange.
- [ ] Minimap (Rautenform, Geländefarben, Einheiten, Sichtausschnitt).
- [ ] Kontrollgruppen (Strg+Zahl), Doppelklick = alle sichtbaren gleichen Typs.

---

## 3. Weitere fehlende Mechaniken

- [ ] **Gegner-KI: null.** Spieler 2 hat Einheiten, aber `UpdateUnits` (`:261`) und
      `DrawUnits` (`:424`) filtern beide auf `OwnerId == 0` — der Gegner ist unsichtbar
      und regungslos.
- [ ] **Gebäude:** `Building` mit `ProductionQueue` existiert als Datenklasse
      (`Data/TileMap.cs:273`), wird aber nie gezeichnet, nie gebaut, die Queue nie
      abgearbeitet.
- [ ] Bauplatzierung durch Dorfbewohner (mehrere Bauende = schneller, abnehmender Ertrag).
- [ ] Mauern und Türme.
- [ ] Siegbedingungen und Spielende — fehlt vollständig.
- [ ] Speichern/Laden — Menüpunkt „Spiel laden" ist ein leerer TODO
      (`Screens/MainMenuScreen.cs:101`).
- [ ] Technologien / Schmiede-Upgrades.
- [ ] Garnison und Reparatur.
- [ ] Geländeeffekte und Erhöhungen.
- [ ] Nachrangig laut Spec: Mönche, Reliquien, Markt/Handel, Zivilisationsboni,
      Seekampf, Kampagnen.

---

## 4. Assets und Aufräumen

- [ ] **Kein einziges AoE-taugliches Grafik-Asset.** Der `RTSGameplayScreen` zeichnet
      farbige 32×32-Quadrate. Blockiert Punkt 7 und 10 der Prioritätenliste.
- [ ] Platformer-Reste entfernen: `Game/Level.cs`, `Game/Player.cs`, `Game/Enemy.cs`,
      `Game/Gem.cs`, `Screens/GameplayScreen.cs`, `Content/Levels/`,
      `Content/Sprites/Player`, `Content/Sprites/Monster*`, zugehörige Sounds.
- [ ] Mobile-Ordner klären: `AgeOfEmpiresClone.Android`, `.iOS`, `.WindowsDX` liegen
      noch im Repo, obwohl `game_plan.md` Desktop-only vorsieht und die Solution sie
      nicht enthält.

---

## 5. README-TODOs (nicht begonnen)

- [ ] UI-Integration (Avalonia/MAUI/MonoGame) — der README beschreibt `AoE.Core` als
      UI-lose Library; faktisch existiert die MonoGame-UI bereits separat.
- [ ] Multiplayer-System
- [ ] `.xws` Scripting-System für KI
- [ ] `.slp` / `.tcx` Dateiformate
- [ ] KI-Hierarchie (Rekrutierung, Strategie)
- [ ] README korrigieren: beschreibt nur `src/AoE.Core` und erwähnt das eigentliche
      Spiel in `AgeOfEmpiresClone/` mit keinem Wort.

---

## Empfohlener nächster Schritt

1. Block 0 entscheiden.
2. Kamera-Koordinatenbug fixen (Block 1).
3. Dorfbewohner-Loop tatsächlich verdrahten (Prio 1).
4. Nahrungsquellen auf die Karte (Prio 2).
5. Ressourcen abbauen statt regenerieren (Prio 6).
