# Feature Notes — aktuelle Änderungen (Stand 2026-10-11)

Unstaged + staged Änderungen, gemessen aus `git diff`/`git diff --cached`.
Die K-Nummern referenzieren die Aufgaben in `tools/ollama-agent/tasks.json` und die Nutzer-Meldungen vom 2026-10-11.

---

## KI-Verhalten (AoE.Core)

### K3 — KI wehrt sich gegen Angreifer, auch fern vom Stadtzentrum
**Problem (Nutzer):** "Wenn ich den Gegner angreife, reagiert er nicht auf die Soldaten, die das Gebäude angreift — er sollte dann meine Soldaten angreifen."

- `EnemyInfo` (IAiWorld.cs): neues Feld `IsAttackingUs` — die feindliche Einheit greift gerade eine eigene Einheit oder ein eigenes Gebäude an, egal wie weit vom TC (Town Center).
- `AiBridge.VisibleEnemies()`: füllt `IsAttackingUs`, wenn die gegnerische Einheit ein eigenes Ziel hat (AttackTarget/UnitTarget mit OwnerId == Owner).
- `EconomyAi.SettlementUnderThreat()`: erkennt neu auch Angreifer außerhalb des alten ThreatRadius (8 Kacheln) — bisher reagierte die KI nur, wenn ein Feind in Sichtweite des eigenen TC war.
- `EconomyAi.CounterAttack()`: greift jetzt immer den Angreifer an, nicht das feindliche TC. Soldaten (Milizen, Bogenschützen) nehmen an der Gegenwehr teil, auch wenn sie gerade laufen (Moving). Dorfbewohner bleiben im Idle-Zustand, wie bisher.
- `EconomyAi.Raid()`:
  - Soldaten, die bereits kämpfen (`HasAttack`), werden nicht mehr neu befohlen — ein neuer Befehl würde den Schlagtakt (2 s Nachladezeit) zurücksetzen und die Truppe würde nie treffen.
  - Dorfverteidigung: Alle Soldaten greifen jetzt den nächsten Eindringling an (Chebyshev-Distanz zum TC), nicht mehr das feindliche TC. Heimwache (HomeGuard) entfällt bei Dorfverteidigung.
  - Zielwelle (K9): Die KI greift zuerst das **nächste sichtbare feindliche Gebäude** an, dann Einheiten — nicht mehr bevorzugt das TC. (Bis K9: Welle ging auf TC oder (0,0), wenn kein TC sichtbar.)

### K8 — Scout kennt die Lage des Gegners nicht mehr
**Problem (Nutzer):** "Aktuell scheint der Gegner zu wissen, wo ich bin, das darf so nicht sein."

Bis K8: Der Scout fuhr geradewegs in die gegenüberliegende Kartecke (voraussichtliche Basis-Position des Gegners, abgeleitet aus der Kartenform).
Seit K8: Der Scout erkundet die **nächste Kachel im Nebel** um die eigene Basis, in Abstandsreihenfolge (quadratischer Abstand, bei Gleichstand kleineres y, dann x). Nicht erreichte Ziele landen in `_scoutUnreachable` und werden übersprungen. Sieht der Scout ein feindliches Gebäude, hält er an — die Soldaten übernehmen (Raid).

Folgewirkung auf die ai-pruefung (tools/ai-pruefung/Program.cs):
- Simulationszeit 600 s → **1200 s** (Scout muss die Basis jetzt finden, ab ~500 s statt ~270 s).
- Punkt 5 (Sieg-Nachweis): "Dorfzentrum fallen muss" → "mindestens **ein Gebäude** zerstört" — ein Spiel auf der kleinen Karte endet nicht mehr sicher in 20 Min mit TC-Verlust, weil der Gegner vorgeschobene Gebäude baut (gemessen: in 3 von 4 Läufen stand nach 1200 s noch jedes TC).

### K9 — Angriffswelle greift Gebäude ab, nicht sofort das TC
**Problem (Nutzer):** "Eventuell sollte der Gegner nicht direkt das Dorfzentrum angreifen, sondern das erste Gebäude, das er sieht."

- `Raid()`-Zielwahl: sichtbare feindliche Gebäude (IsBuilding) zuerst, nächstes zur Truppe (Chebyshev, Basis = soldaten[soldiers.Count/2]); erst wenn kein Gebäude sichtbar, die nächste Einheit. IsTownCenter spielt keine Rolle.
- In AoE.Core/EconomyAi.cs (Raid-Zielwahl), nicht im Screen.

### Wirtschaftliche Stellschrauben (EconomyAi.cs)
- `MaxQueue`: 12 → **2** — eine lange Schlange bindet Nahrung (25/Kopf), die Miliz (60) und Aufstieg (500) brauchen.
- `BuildSpotRadius`: 12 → **20** — in der Kartenecke war der Umkreis 12 nach wenigen Häusern voll; jetzt wird von innen nach außen gesucht.
- `W_DARK` (Dunkle Zeit): gold 0 → **15 %** — Gold ist jetzt in der Dunklen Zeit verfügbar, damit die Kaserne (Miliz kostet 20 Gold) nicht leersteht.
- Farm-Schwelle: `FarmMinimumFood` → **max(FarmMinimumFood, Nahrungskosten nächsten Zeitalters + 100)** — die Nahrung muss für den Aufstieg mit Reserve ankommen.
- `allowPool`: Wenn kein Dorfbewohner Nahrung sammelt, darf die Farm einen Bauern aus dem Sammelpool ziehen (allowPool: true); solange jemand sammelt, bleibt allowPool: false, damit die Ernte nicht unterbrochen wird.

---

## GUI / Spieler-Interaktion (RTSGameplayScreen.cs)

### K5 — Gegner anklicken: Trefferfläche größer
**Problem (Nutzer):** "Wenn ich einen Gegner angreifen möchte … kann ich ihn nur schwer auswählen. Es scheint so, dass ich ihn am Fuß auswählen muss."

- `UnitWorldRect()`: Berücksichtigt jetzt den `Scale` aus SoldierSprites (Späher zu Pferd = 1,35) — die Figur ragt 24 px nach oben, der Späher 32 px.
- `EnemyUnitUnder(worldPos)`: neue Methode — sucht die feindliche Einheit, deren Figur unter dem Klickpunkt liegt. Trefferfläche = `UnitWorldRect` + `ENEMY_CLICK_SLACK` (4 px) Inflate. Sieht der Spieler sie (IsTileVisible), ist sie trefferbar. Bei mehreren: gewinnt die größte Position.Y (vorn gezeichnet), dann die spätere in units.
- `LeftClick()`: Mit ausgewählten Einheiten + Klick auf die Figur eines sichtbaren Gegners → `AttackUnit(selectedUnits, Gegner)`. (K5b: auch wenn ein eigener Kämpfer davor steht — bis K5b wählte ein Klick auf einen eigenen Kämpfer vor dem Gegner ihn aus, man konnte keinen zweiten zu Hilfe schicken.)
- `LeftClick()`: Mit ausgewählten Einheiten + Klick auf ein fremdes Gebäude (auch aufs Dach, `BuildingUnder` mit OwnerId ≠ 0) → `AttackBuilding(selectedUnits, Gebäude)`.
- `AttackUnit()`: Wenn `FindPath` keinen Weg liefert, aber das Ziel in Reichweite ist (`InAttackRange`, z. B. dieselbe Kachel) → sofort `Attacking`. Sonst: `UnitTarget = null`, `State = Idle` — die Einheit bleibt nicht untätig mit gesetztem Ziel stehen.
- `AttackBuilding()`: Ist der Weg leer, steht die Einheit aber auf der Kachel des Standplatzes → ein einziger Wegpunkt (FindPath kennt keinen Weg innerhalb einer Kachel).
- `InAttackRange()`: neue private Methode — Chebyshev-Distanz in Kacheln, Reichweite `Math.Max(1, unit.AttackRange)`. In `HandleAttack` durch Aufruf ersetzt (bisher inline).

### K6 — Soldaten verteidigen das Stadtzentrum automatisch
**Problem (Nutzer):** "Zudem sollten Soldaten jemanden, der das Dorfzentrum angreift, automatisch angreifen."

- `DefendTownCenters(dt)`: neue Methode, in `UpdateUnits()` zu Beginn jedes Bilds aufgerufen, für beide Spieler (Owner 0 und 1).
  - Interval: `TOWN_DEFENSE_INTERVAL` = 0,5 s (Timer `_townDefenseTimer`).
  - Angreifer: Einheiten mit `State = Attacking`, deren `AttackTarget` ein TC (Core.BuildingType == TownCenter) eines anderen Besitzers ist, das noch in `tileMap.Buildings` steht.
  - Verteidiger: Einheiten des TC-Besitzers, `AttackPower > 0`, keine Dorfbewohner, State Idle oder Guarding, keine UnitTarget/AttackTarget/Job/BuildSite, Distanz zum TC-Mitte ≤ `TOWN_DEFENSE_RADIUS` (15 Kacheln × TileSize, Luftlinie).
  - Jeder Verteidiger greift den nächsten Angreifer an (MinBy DistanceSquared).
  - Wer läuft/kämpft/sammelt/baut, bekommt keinen Befehl — Spielerbefehle haben Vorrang.
- `TOWN_DEFENSE_RADIUS` = 15 Kacheln, `TOWN_DEFENSE_INTERVAL` = 0,5 s (Konstanten).

### K7 — Schlagbild bei Einheiten-Kampf
**Problem (Nutzer):** "Wieso wippt der Soldat, wenn er einen gegnerischen Dorfbewohner angreift, anstatt wie bei einem Gebäude die Waffe zu schwingen?"

- `IsStriking(unit)`: neue Methode — true, wenn `State = Attacking` UND (AttackTarget ≠ null ODER (UnitTarget ≠ null und nicht Dead)). Bisher: nur Gebäude als Ziel.
- `DrawVillager()`: `striking = !moving && attack != null && IsStriking(unit)` — die Schlagbilder (C4n) werden jetzt auch bei Einheiten-Kampf verwendet.
- `UnitMotion`: bei `State = Attacking` und `UnitTarget` → Blickrichtung zum Gegner (Blick-Korrektur).

---

## Build / Infrastruktur

### Directory.Build.props (neu, staged)
- `MonoGameVersion` = 3.8.6 — zentral definiert, damit alle Projekte (Core, DesktopGL, WindowsDX, tools) dieselbe MonoGame-Version verwenden. Bisher: `3.8.*` in jedem csproj separat → Risiko, dass Core eine neuere MonoGame kompiliert als die App mitbringt → FileLoadException beim Start.

### Modelfile (neu, staged)
- Ollama-Modell für die KI-Prüfung: `qwen3.8:27b-q8_0`, `num_ctx = 256000`, KV-Cache `q4_0`.

### MonoGame.csproj (Core + DesktopGL + WindowsDX)
- MonoGame.Framework.Native: bisher `3.8.*` → jetzt `$(MonoGameVersion)`.
- DesktopGL/WindowsDX: bisher je eigene `3.8.*` → jetzt `$(MonoGameVersion)`.
- Core.csproj: `PrivateAssets=all` auf Native-Paket (kein Runtime-Flow an die Apps), `ExcludeAssets=runtime;content` entfernt (Core kompiliert gegen Native, die Apps bringen ihre Plattform-MonoGame selbst mit).

---

## Tests & Abnahme-Tools

### tools/spielablauf/Program.cs (unstaged)
Neue Prüfgruppen:
- **zielen** (K5): 7 Testcases — Brust, Seite, über dem Kopf, im Nebel, Späher am Kopf, eigener Kämpfer vor dem Gegner (K5b), Gegner nach Klick besiegt.
- **verteidigung** (K6): Greift ein Gegner ein TC an, greifen die untätigen Soldaten in der Nähe von selbst an — auf beiden Seiten. Dorfbewohner nicht, Soldaten weit draußen nicht, wer einen Befehl ausführt, bleibt dabei.
- **kampfbild** (K7): Schlagbilder (Schwingen) bei Einheiten-Kampf; Blickrichtung zum Gegner.
- **niederlage**: neu in den Gruppen.

### tools/ai-pruefung/
- **gegenwehr** (K3, neu): Je Karte (3 frisch erzeugte): ein Haus der KI 12–14 Kacheln vom TC, 2 Milizen des Spielers greifen an, 2 KI-Milizen am TC. Binnen 30 s muss: eine KI-Miliz einen Angreifer als Ziel haben + ein Angreifer LP verlieren.
- **entwicklung** (neu): Die KI allein, 20 Min Spielzeit, 3 Karten. Ab Minute 3: nie zwei Minuten hintereinander dieselben Vorräte und dieselbe Bevölkerung (Stillstands-Erkennung). Am Ende: mindestens Feudalzeit + mindestens `RaidMinSoldiers` Soldaten. Vorher: Dunkle Zeit nach 20 Min, max. 5 Milizen, teilweise Stillstand ab Minute 4.

### tests/AoE.Tests/AI/EconomyAiTests.cs (unstaged)
Neue Tests (Abschnitt "Gegenwehr", "Entwicklung", K8, K9):
- K3 (Gegenwehr): `SoldiersDefendAgainstAttackerFarFromTownCenter`, `CounterAttackTargetsAttackerNotEnemyTownCenter`, `MovingSoldierJoinsCounterAttack`, `FightingSoldierIsNotReorderedInCounterAttack`, `GatheringVillagerStaysWhenAttackerFarAway`, `HomeDefenseTargetsIntruderNotEnemyTownCenter`, `HomeDefenseDoesNotReorderFightingSoldier`, `HomeDefensePrefersUnitOverCloserBuilding`
- Entwicklung: `VillagerQueueStaysShort` (MaxQueue 2), `DarkAgeSendsWorkersToGold` (Gold in der Dunklen Zeit), `NewWorkerGoesWhereFewestAlreadyGather`, `AgeUpNeedsRaidGroupNotFullArmy`, `BuildsFarmsUntilAgeUpFoodIsReached` (Farm-Schwelle = Aufstieg+100), `NobodyOnFoodPullsGathererOntoFarm` (allowPool), `NewBuildingKeepsOneTileFromOwnBuildings`
- K8 (Scout): `ScoutExploresNearestFogInsteadOfEnemyCorner`, `ScoutKeepsTargetWhileUnderway`, `ScoutPicksNextFogTileWhenTargetExplored`, `ScoutGivesUpUnreachableTarget`, `ScoutHoldsWhenEnemyBuildingVisible`, `ScoutStaysWhenMapExplored`
- K9 (Raid): `RaidTargetsNearestBuildingNotTownCenter`, `RaidPrefersBuildingOverUnit`, `RaidTargetsNearestUnitWhenNoBuildingVisible`
- FakeWorld-Erweiterungen: `MarkNotExplored`/`MarkExplored(x, y)` (K8), `AddExploreTarget`, `AddSource`, `AddEnemy`, `AddSoldier(x, y, s, hasAttack)` (Staat/HasAttack für K3).

### tools/klang/ (neu, staged)
- `klang.py`: synthetische Geräusche (Arbeit + Kampf) mit numpy/scipy — gedämpfte Eigenschwingungen, gefiltertes Rauschen, Karplus-Strong für Bogensehne. 3 Varianten pro Geräusch (verschiedene Zufallssamen). Ausgabe: 16-bit mono WAV, 44,1 kHz. `--vorschau` für Vorschaudatei.

---

## Zusammenfassung der Nutzer-Feedbacks (2026-10-11)

| ID | Feedback | Fix |
|----|----------|-----|
| K3 | "KI reagiert nicht auf Angreifer fern vom TC" | Gegenwehr auch bei IsAttackingUs, Ziel = Angreifer nicht TC |
| K5 | "Gegner nur schwer anklickbar" | EnemyUnitUnder + ENEMY_CLICK_SLACK, Klick greift an |
| K5b | "Eigener Kämpfer davor wählt sich, kein 2. zu Hilfe" | LeftClick: EnemyUnitUnder VOR OwnUnitAt |
| K6 | "Soldaten sollten TC angreifen" | DefendTownCenters, 0,5 s Interval, 15-Kachel-Radius |
| K7 | "Soldat wippt statt Waffe schwingen" | IsStriking mit UnitTarget, Schlagbilder bei Einheiten |
| K8 | "KI weiß, wo ich bin" | Scout erkundet Nebel, nicht gegenüberliegende Ecke |
| K9 | "Gegner soll erstes Gebäude angreifen" | Raid: Gebäude zuerst, TC keine Priorität |
