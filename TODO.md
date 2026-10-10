# AoE-Clone — TODO / Meilensteine

**Stand:** 2026-10-10 · verifiziert durch Build + Testlauf, nicht aus Doku übernommen.

## Wiederaufnahme — hier weitermachen

Zuletzt angefasst: **Unter-Angriff-Erkennung, automatisch zurückschlagen, Alarm** (2026-10-10) — Feedback: der gegnerische Dorfbewohner marschierte bis ins eigene Dorf, ohne dass die KI reagierte; dazu: Sound + oberer Balken kurz rot bei Angriff auf unsere Seite.
(1) **„Unter Angriff" als Welttatsache:** neue `IWorldState.LastDamageAt` + `WorldTime` (Sekunden) — der Screen merkt sich pro Spieler den letzten Treffer auf eigene Einheiten/Gebäude (`RegisterDamageTaken` an beiden Schlag-Stellen: `Unit.Attack` und `BuildingCombat.Hit`). `AiBridge.WorldTime` = `Screen.GameSeconds`, gleiche Skala. Die Siedlung ist "unter Angriff", wenn ein sichtbarer Feind in `ThreatRadius=8` (Chebyshev) am Dorfzentrum steht ODER frischer Schaden (< 6 s) + sichtbarer Feind.
(2) **Automatische Gegenwehr:** `EconomyAi.SettlementUnderThreat()` → `CounterAttack()`: alle ungebundenen Einheiten (Idle, kein Sammel-/Bau-Auftrag, `!HasAttack`, auch Scout — im Ernstfall zählt jeder) greifen sofort an: ohne Mindestgröße, ohne Cooldown. Neue `UnitSnapshot.HasAttack` (AttackTarget/UnitTarget setzen) verhindert doppeltes Befehlen, das den Schlag-Timer zurücksetzen würde.
(3) **Alarm (nur Seite 0):** `RaiseUnderAttack()` bei jedem Treffer auf Spieler-0-Einheiten/Gebäude: obere Leiste glüht ~2,5 s rot (Puls ~1 Hz, ausklingend), Sound `Sounds/Alarm` (neues WAV: 660→880 Hz, 0,32 s, ffmpeg erzeugt), Cooldown 3 s damit Dauerfeuer nicht piept. Defensive `LoadContent` — fehlt das Asset, bleibt nur das rote Blinken.
(4) **Zieh-Angriff:** `StepAlongPath` plant bei Angriffszielen jetzt per Takt neu auf die aktuelle Ziel-Kachel (Ziele laufen weg — Bogenschützen, Dorfbewohner), sonst läuft man den ersten Weg ins Leere (das "Attack auf (5,5)"-Symptom).
Abnahme: `dotnet build` (0 Fehler) · `dotnet test` (278/278, +3 neue: `VillagersCounterAttackWhenSettlementThreatened`, `NoCounterAttackWhenNoDamageTaken`, `NoCounterAttackWhenDamageIsOld`) · `tools/ai-pruefung` → **alle Prüfungen ok, Sieg t=420 s** (TC fiel). Sound + rotes Blinken nur im spielbaren Window (headless läuft ohne `LoadContent`) — per Augen checken.

Davor: **KI: Mühle/Holzfällerlager an der Ressource, Dorfbewohner verteidigen, Niederlage beim Dorf** (2026-10-10) — drei zusammenhängende KI-/Spiele-Ende-Änderungen, alle drei verifiziert.
(1) **Gebäude-Anchor:** `EconomyAi.cs` holt jetzt für Holzfällerlager→Wood, MiningCamp→Stone/Gold, Farm→Food eine `state.FindSource(res, tcCenterX, tcCenterY, AnchorSearchRadius=20)` und ruft `TryBuildNear(..., anchor)` mit der Quellemitten-Kachel als Ringzentrum; ein Footprint-Guard verhindert, dass ein 2×2-Lager die Quell-Kachel überdeckt. Fehlt die Quelle >20 Kacheln vom TC entfernt (oder existiert keine), baut die KI dort kein Lager mehr — der sinnlose TC-Bauboom ist weg.
(2) **Dorfbewohner-Verteidigung:** neue `Defense()`-Methode, in `Tick` nach `Raid()` aufgerufen; für jeden sichtbaren Feind greifen untätige (nicht sammelnde/bauende/scoutende) Dorfbewohner in `DefenseRadius=8` an, max. `DefenseMax=5` Einheiten/Tick. `IWorldActions.Attack` ist der Kanal; der Screen führt den Angriff wie bei `IssueCommand`.
(3) **Niederlage:** `RTSGameplayScreen.DefeatRule(units, buildings, ownerId)` — statische Kernregel, testbar ohne MonoGame: ein Spieler ist geschlagen, wenn er keinen LEBCNDEN Dorfbewohner UND kein Stadtzentrum (fertig oder im Bau) mehr hat. `CheckDefeat()` wird nach `UpdatePopulationLimits()` im Frame aufgerufen, Guard-Flag `_gameOverShown` stellt sicher, dass die End-Meldung und `ScreenManager.AddScreen(new GameOverScreen(...), null)` einmalig sind. Neuer Screen `AgeOfEvolutions/AgeOfEvolutions.Core/Screens/GameOverScreen.cs`: Titel + Detail, Enter/Klick → `LoadingScreen.Load(..., new MainMenuScreen())`. Headless-Tests ohne ScreenManager werden in `CheckDefeat` in try/catch abgefangen, die Regel selbst bleibt lauffähig.
Abnahme: `dotnet build` (0 Fehler) · `dotnet test` (274/274, +7 neue) · `tools/ai-pruefung` (6/6: beide KI aktiv, Militär, Erkundung, Angriff) · `tools/spielablauf -- niederlage` (3 Karten: Leute+TC ⇒ weiter, nur TC weg ⇒ noch weiter, beides weg ⇒ verloren + HUD-Meldung "P1 hat verloren — P2 gewonnen!").

Davor: **DX ↔ GL Größenangleichung (DPI) und +80 % Rohstoffsymbole**
(2026-10-10) — Der DirectX-Build war still DPI-aware (`GetDpiForWindow=192`
bei System-200 %): Windows hat das Fenster nicht mit-bildvergrößert und die
UI stand auf 1:1 physische Pixel — die GL-Version ist DPI-unaware
(`win_dpi=96`) und wird von Windows bei 200 % 2× bitmap-vergrößert.
Ursache: `System.Windows.Forms.Primitives.dll` (in
`Microsoft.WindowsDesktop.App/10.0.12/shared/`) ruft
`SetProcessDpiAwarenessContext` automatisch auf, weil der WindowsDX-csproj
`<UseWindowsForms>true</UseWindowsForms>` gesetzt hat. Fix: explizit
`<dpiAware>false</dpiAware>` in BEIDEN `app.manifest` (DesktopGL + WindowsDX)
unter `<application>/<windowsSettings>`; das Manifest hat Vorrang vor dem
auto-DPI-Kontext. Danach: beide `win_dpi=96`, pixelgleich.
`RESOURCE_ICON_SIZE` 26 → 47 px (+80 %), `HUD_TOP_HEIGHT` 34 → 56 px damit die
Icons Platz haben; obere Leiste (Füllfarbe, Kante, Rohstoffwerte rechts,
Bev./Zeitalter rechts, Hinweis/Ausbildung Mitte) zieht auf die neue Höhe,
alle Texte von `y=6` auf die Leistenmitte. `IsOverHud` und
Kamera-Randberechnungen nutzen `HUD_TOP_HEIGHT` weiter dynamisch.
**Release-Builds und `release/*.zip` (v0.3.3, 09.10.) sind veraltet** —
vor dem Icons-/Leisten-Feature und vor dem DPI-Fix. Vor Abgabe neu bauen
(siehe aoe-release-builds).
Abnahme: `dotnet build` + `dotnet test` (267), `tools/spielablauf --
leiste menue` (3 Karten), `GetDpiForWindow` für GL und DX = 96
(beide, gemessen mit ctypes/user32).

Davor: **KI: beide Seiten spielen, erkunden, militärisch, aggressiv** (2026-10-10) —
zwei KI-Instanzen (Owner 0 und 1) laufen gleichzeitig
(`BothSidesAi` an der Screen, `AiAgent` pro Owner). Jede KI führt einen
festen Erkundungs-Scout (Dorfbewohner, aus dem Bau-/Ernte-Pool
herausgenommen) zur gegenüberliegenden Kartecke, bis die gegnerische
Basis sichtbar ist; Kaserne + Schießstand werden ab 150/175 Holz gebaut;
Soldaten (Milizen/Bogenschützen) werden in der Kaserne/Schießstand
ausgebildet und greifen sichtbar feindliche **Einheiten oder Gebäude**
an. **Einheiten-Gegen-Einheiten-Kampf ist neu:** `Unit.Attack(defender)`
existierte schon, aber es gab kein Angriffszugriff, keine Zielzuweisung
und keinen Aufräumer. Neu: `EnemyUnitAt()`, `AttackUnit()`,
`DestroyUnit()` (Pop + Listen), `HandleAttack()` (Schlagtakt,
Reichweiten-Check, Nachlaufen). `IssueCommand` greift sichtbare
feindliche Einheiten auf der Zielkachel an. Tote Einheiten werden am
Ende von `UpdateUnits` entfernt (nicht während der Iteration).
Abnahme: `tools/ai-pruefung` (6 Prüfungen), `tools/spielablauf
-- soldaten linksklick` (3 Karten).

Davor: **Wiese in drei Höhen, Beerenbüsche, Rohstoffsymbole** (2026-10-10, G12, G13, R1) —
fünf Grassorten von kurzem Kleegras bis hüfthohem Gras plus ausgedörrte Erde, im hohen Gras
stehende Büschel, in denen Figuren bis zur Hüfte stecken; Beerenbüsche und die vier
Rohstoffsymbole oben als Bilder. Davor **Bäume, Gras und Leisten** (2026-10-09, T1b, G11, H1) — acht Baumarten mit
offener Krone, einzeln in der Tiefenschicht und etwas größer; feineres Gras; die Leisten im Stoff
des Zeitalters. Dazu die **Durchsicht der Tiefenschicht** (T1a). Davor **Schwertschlag und
Speerstoß** (C4n) — die Miliz holt beim Angriff
aus und schlägt über den Kopf zu, der Späher stößt mit gesenktem Speer; der Treffer im Bild fällt
genau auf den Abzug der Stärke. Davor **Natürliches Gehen** (C4l, C4m) — alle Figuren gehen in
acht Phasen je Doppelschritt, ohne dass die Füße rutschen. Davor **Forschungen** (2026-10-09, P2) — ein ausgewähltes Gebäude forscht: Webstuhl im
Stadtzentrum (W), Pferdekummet, Doppelaxt und Goldbergbau in den Lagern (Q), drei Forschungen in
der Schmiede (Q, W, E), Maurerkunst in der Universität (Q) —, **Soldaten** (P1; committet mit
`ad2ca62`) und **Gebäude auswählen und angreifen** (2026-10-06, B1, K1, K2). Handel fehlt noch.
G12, G13 und R1 sind uncommittet im Arbeitsbaum; Commit macht der Nutzer.

Alles baut und läuft: `dotnet build AgeOfEvolutions.slnx` (0 Fehler; Warnungen: NU1902/NU1903
zu SixLabors.ImageSharp, das MonoGame 3.8.6 mitbringt, dazu CS8766 und xUnit2029 in
`tests/AoE.Tests/AI/EconomyAiTests.cs`), `dotnet test tests/AoE.Tests` → **267/267 grün**.

Der Stand vom 2026-09-23 ist mit `4cb57d4` committet, alles bis zu den Gebäude-Sprites
mit `4f85314` (2026-10-03). Commits macht der Nutzer selbst.

Zuletzt fertiggestellt:

- **Wiese in drei Höhen, ausgedörrte Stellen, Beerenbüsche, Rohstoffsymbole (2026-10-10)** —
  Der Nutzer fand das Gras nach G11 zu eintönig, wie frisch gemäht: Gras sei zwischen kurz und
  hüfthoch (etwa ein Drittel eines Dorfbewohners), drei bis vier Arten wären gut, manche Stellen
  ausgedörrt kahl; dazu die Beerenbüsche in hoher Qualität und die Rohstoffsymbole oben als
  Bilder, doppelt so groß. Boden: sechs Flächen per Qwen-Image (1328 px, 50 Schritte, je zwei
  Seeds): ungemähte Wiese, kurzes Kleegras, hohes Gras, hohes trockenes Gras, Blumenwiese und
  rissige Erde (`Boden/gras_hoch`, `Boden/erde_trocken` neu); `Meadow` überblendet sie in einem
  schmalen Fenster nach Halmhöhe, das hohe Gras wächst, wo `TallGrassAt` es sagt (Flecken aus
  Wertrauschen, nur auf freier Wiese, auf Trampelpfaden niedergetreten; Alphakanal des
  Lebendbilds). Höhe: im hohen Gras stehen Büschel als eigene Einträge der Tiefenschicht (bis 16
  je Kachel, 8 Welteinheiten hoch, im Wind; `TuftSpot`, `DrawTuftAt`, `Gras/bueschel_*`), und eine
  Figur im hohen Gras bekommt drei Büschel vor die Füße (`DrawGrassAroundFeet`) - sie steckt bis
  zur Hüfte darin. Unter 10 Bildpunkten Büschelhöhe nur die Textur. Gemessen (Release, 3008 x 1692):
  60 Bilder je Sekunde, `DrawUnits` 4,3 ms bei Zoom 1,5 statt 0,6 ms ohne Büschel. Beerenbüsche:
  Beerenstrauch, Johannisbeere, Brombeere (`Nahrung/beerenbusch*`, `DrawBerryBush`), stehend in der
  Tiefenschicht statt der gezeichneten Kachel. Rohstoffsymbole: Keule mit Brot und Beeren, Holzstapel,
  Goldhaufen, Steinhaufen (`Icons/rohstoff_*`, `ResourceIcons`, 26 statt 12 px). Code vom lokalen
  Modell: R1 im ersten Lauf, G12 im zweiten (zwei Typen namens `Tile`; die Startprobe brach einmal
  ohne Fehler ab, die Fassung lief danach zweimal 10 s), G13 im dritten (falscher Listenname,
  überzähliges Argument); die Läufe in einer Spiegelkopie, weil das Spiel aus Rider die Exe
  sperrte. Abnahme `g12_wiese.py`, `g13_bueschel.py`, `r1_rohstoffe.py` *(G12, G13, R1)*
- **Bäume mit offener Krone, feineres Gras, Leisten je Zeitalter (2026-10-09)** — Wunsch des
  Nutzers: die Bäume in hoher Qualität neu, so offen wie die Tannen, damit sie nicht alles
  dahinter verdecken, und etwas größer; das Gras feiner und im Verhältnis zu den Bäumen kleiner;
  die untere Leiste im Stoff der Epoche. Acht Baumarten (zwei Eichen, Ahorn, Buche, Birke,
  Fichte, Tanne, Kiefer) per Qwen-Image in 1104 x 1472 mit 50 Schritten, je Art drei Seeds zur
  Wahl, freigestellt und 512 Pixel breit; weiße Lücken in der Krone macht `uebernehmen.ps1`
  (Option `loecher`) ohne hellen Saum durchsichtig. Im Spiel ist jeder Baum ein eigener Eintrag
  der Tiefenschicht mit seinem Stammfuß als Sohle (`TreeSpot`, `DrawTree`), zwei je Waldkachel
  statt drei, 58 bis 86 Welteinheiten hoch (`TreeAssets`; ein Dorfbewohner ist 24), Laub- und
  Nadelbäume in Hainen von etwa 6 x 5 Kacheln. Gras: die vier Sorten neu in 1328 Pixeln mit viel
  feineren Halmen, je Farbkanal großflächig ausgeglichen (Option `ausgleichen`, sonst zeigte das
  gekachelte Bild ein Raster), im Bodenshader mit eigenem Maßstab `GRASS_TEXELS` = 7 statt 4
  (`GrassScale`), Sand und Wasser unverändert; die Mittelwerte der Sorten liegen auf den alten,
  die Abstimmung aus G10 gilt weiter. Leisten: rohe Holzbohlen, Eichenbohlen, Quadermauer, grüner
  Marmor mit Goldkante (`Leiste/*`, `DrawPanel`, `PanelEdge`) oben, unten und hinter der Minimap;
  ein dunkles Feld hält den Hilfetext auf Stein und Marmor lesbar. Code vom lokalen Modell (je
  10 bis 31 s, T1b im zweiten Lauf); Abnahme `t1b_baeume.py`, `g11_gras.py`, `h1_leiste.py`
  *(T1b, G11, H1)*
- **Durchsicht der Tiefenschicht (2026-10-09)** — fünf Befunde am Umbau `6def1ec` behoben: die
  Lebensbalken der Gebäude lagen unter Bäumen und Gebäuden (jetzt in `DrawUnits` über allen
  Bildern); `List.Sort` ist nicht stabil, überlappende Bäume einer Zeile konnten von Bild zu Bild
  tauschen (Eintragsindex `Seq`); Tiere wurden nach der Zielkachel statt nach den Hufen sortiert
  (`AnimalFoot`, auch mitten im Schritt); `OwnUnitAt` nahm die letzte statt der vordersten Figur;
  `DrawUnits` lief je Bild über die ganze Karte (jetzt nur Kacheln nahe am Fenster). Außerdem
  hing `d21_doku.py` stundenlang (Regex mit exponentiellem Backtracking), und `c7r_boden.py` hatte
  die Prüfung verloren, dass `DrawTileMap` keine Bodenkacheln mehr zeichnet. Abnahme
  `t1a_tiefe.py` *(T1a)*
- **Bäume verdecken Gebäude, Gebäude Bäume (2026-10-09)** — die Zeilenordnung
  beim Zeichnen war bisher: alle Bäume, dann alle Haufen, dann alle Gebäude,
  zuletzt alle Figuren; ein Baum weiter unten im Bild konnte deshalb ein
  Gebäude weiter oben überdecken, und umgekehrt nicht. Jetzt stehen Bäume,
  lebende Tiere, Gebäude und Figuren in einer gemeinsamen Tiefenschicht
  (`DrawUnits`): jedes Objekt trägt die Bildschirm-y seiner Sohle (Bäume
  Stammfuß, Tiere Hufe, Gebäude untere Grundflächenkante, Figuren die Füße),
  die Liste wird sortiert und in dieser Reihenfolge gezeichnet — wer weiter
  unten steht, verdeckt, was weiter oben steht, in beide Richtungen. Stein-
  und Goldhaufen, Felder und das Fleisch erlegter Tiere sind flach am Boden
  und man läuft darüber, deshalb bleiben sie im Bodendurchlauf (`DrawTileMap`)
  und nehmen nicht an der Tiefenordnung teil; sie verdecken nichts, was
  dahinter steht. Nebel, Auswahlrahmen, Lebensbalken und Leiste liegen wie
  zuvor darüber bzw. darunter. Abnahme `c7r_boden.py`, `g3_kronen.py`,
  `c7t_tiere.py`, `c7s_fleisch.py` *(Tiefenschicht)*
- **Schwertschlag und Speerstoß (2026-10-09)** — greift die Miliz ein Gebäude an, zeigt sie acht
  Schlagphasen statt des Kippelns der ganzen Figur: bereit, ausholen, die Klinge hinter dem Kopf,
  über den Kopf, Hieb, Treffer, durchziehen, zurücknehmen; der Späher senkt seinen Speer und
  stößt ihn durch die Faust nach vorn. `tools/bilder/schlag.py` schneidet Waffenarm (Miliz) bzw.
  Speer (Späher) aus dem Standbild und dreht sie um Schulter, Ellbogen oder Faust über einem
  Rumpf ohne Waffe, den Qwen-Image einmal neu gemalt hat; die Schulterkappe liegt über dem Arm,
  über dem Kopf geht er hinter dem Helm durch. Die Schlagbilder haben eine größere Leinwand im
  Maßstab des Standbilds, die Füße stehen an derselben Stelle. Im Spiel wählt `AttackPhase` das
  Bild nach `AttackTimer` (Tabelle `AttackTimeline`), das Trefferbild steht genau, wenn
  `BuildingCombat` die Stärke senkt. `docs/bilder/schlag.gif` zeigt beide in Zeitlupe; Abnahme
  `c4n_schlag.py`, Gruppe `schlag` in `tools/spielablauf` *(C4n)*
- **Natürliches Gehen (2026-10-09)** — Dorfbewohner aller Zeitalter, Miliz, Bogenschütze und
  Späher gehen in acht Phasen je Doppelschritt statt in zwei Laufbildern mit dem Standbild
  dazwischen. `tools/bilder/gang.py` schneidet die Beine des Standbilds in Oberschenkel,
  Unterschenkel und Fuß und stellt sie nach Fußbahnen: der Standfuß ruht und wandert unter dem
  Körper nach hinten, setzt mit der Ferse auf und rollt über die Zehen ab, der Schwungfuß zieht
  im Bogen nach vorn, das Knie ergibt sich aus den Knochenlängen, der Körper wippt dabei mit.
  Das Pferd des Spähers geht im Viertakt, die Sprunggelenke knicken nach hinten. Rumpf,
  Kleidung und Waffe bleiben Pixel für Pixel; Gelenkscheiben halten Knie und Knöchel bei jedem
  Winkel geschlossen. Dafür wurden die Beine der Imperialzeit neu gemalt (die Hose endete neben
  dem Stiefel, C4l) und Miliz und Bogenschütze in Schrittstellung gestellt, beides per
  Qwen-Image nur unter einer Beinmaske. Im Spiel wählt `Gait.WalkFrame` die Phase nach der
  gelaufenen Strecke, `Gait.VILLAGER_STRIDE` (5,06 Welteinheiten) ist die Schrittlänge der
  Bilder; Werkzeug und Traglast gehen mit dem Wippen (`VillagerWalkHub`). Ausbessern der
  Gelenke mit Qwen-Image war verworfen: es malte die Stiefel von Phase zu Phase anders.
  `docs/bilder/gang.gif` zeigt alle Figuren in Zeitlupe; Abnahme `c4m_gang.py`, Gruppen `gang`
  und `gehen` in `tools/spielablauf` *(C4l, C4m)*
- **Forschungen (2026-10-09)** — ein ausgewähltes eigenes Gebäude forscht mit der Taste aus
  der Tabelle `Researches`: das Stadtzentrum den Webstuhl (W, 50 Gold, 25 s; Dorfbewohner +15 LP,
  Rüstung +1), die Mühle das Pferdekummet (neue Felder 250 statt 175 Nahrung), das
  Holzfällerlager die Doppelaxt (Holz 20 % schneller), das Bergbaulager den Goldbergbau (Gold
  15 % schneller), die Schmiede Schmiedekunst, Befiederte Pfeile und Schuppenpanzer (Q, W, E;
  Angriff, Reichweite, Rüstung von Miliz, Späher und Bogenschütze), die Universität die
  Maurerkunst (Gebäude +10 % LP, Rüstung +1). Kosten, Dauer, Zeitalter und Wirkung nach AoE II
  stehen in AoE.Core (`Economy/Research.cs`: `TechRules`, `TechProgress`, `ResearchSlot`,
  `TechEffects`); der alte, nie benutzte `TechTree` ist weg. Bezahlt wird beim Start, ein Gebäude
  forscht eine zur Zeit und bildet so lange nicht aus, verschiedene Gebäude forschen
  gleichzeitig; die fertige Forschung wirkt auf alles, was der Spieler hat und noch bekommt.
  Die Leiste zeigt „forscht: Name %“; wird ein forschendes Gebäude zerstört, kommen die Kosten
  zurück. 43 neue Tests (`ResearchTests`), Gruppe `forschung` in `tools/spielablauf`, Abnahme
  `p2_forschung.py` *(P2a, P2b)*
- **Soldaten (2026-10-09)** — ein ausgewähltes eigenes Gebäude bildet aus, was die Tabelle
  `Products` sagt: das Stadtzentrum Dorfbewohner, die Kaserne die Miliz (60 Nahrung, 20 Gold,
  21 s), ab der Feudalzeit der Schießstand den Bogenschützen (25 Holz, 45 Gold, 35 s) und der
  Stall den Späher (80 Nahrung, 30 s) - je mit Taste Q in der Leiste und auf der Tastatur,
  Kosten und Zeiten nach AoE II. Die Soldaten erscheinen am Gebäude, gehen wie Dorfbewohner
  und tragen ihre Waffe im Bild (Qwen-Image, Stand und zwei Laufbilder je Spielerfarbe, der
  Späher zu Pferd etwas größer); sie sammeln nicht, greifen aber fremde Gebäude an. Solange ein
  Gebäude ausgewählt ist, schalten die Bautasten nicht in den Setzmodus. Gruppe `soldaten` in
  `tools/spielablauf`, Abnahme `p1_soldaten.py` *(P1)*
- **Gebäude angreifen (2026-10-06)** — ein Linksklick mit eigenen Einheiten auf ein erkundetes
  fremdes Gebäude schickt sie zum Angriff: sie stellen sich an die Gebäudekante (`AttackStand`)
  und schlagen alle 2 s zu. Die Stärke sinkt nach `BuildingCombat` in AoE.Core (Nahkampf gegen
  die Rüstung, Pfeile prallen fast ganz ab), bei 0 ist das Gebäude zerstört und seine Kacheln
  sind frei (`TileMap.RemoveBuilding`). Jeder andere Befehl bricht den Angriff ab, eigene
  Gebäude greift ein Klick nicht an; die KI greift noch nicht an. Gruppe `angriff`, Abnahme
  `k2_angriff.py` *(K1, K2)*
- **Gebäude auswählen (2026-10-06)** — ein Klick wählt ein Gebäude aus: Rahmen, Lebensbalken
  und in der Leiste der Status (Stärke, Ausbildung, Aufstieg oder Baufortschritt; fremde
  Gebäude nur, wenn erkundet). Q und A wirken auf das ausgewählte Stadtzentrum, ein zweites
  bildet selbst aus. Gruppe `auswahl`, Abnahme `b1_auswahl.py` *(B1)*
- **README-Bilder (2026-10-06)** — Spielszene, Karte und Zeitalter neu aufgenommen, mit der Wiese
  aus vier Grassorten und einem ausgewählten Stadtzentrum samt Status; neu `docs/bilder/gebaeude.jpg`:
  alle Gebäude der Imperialzeit mit Mauerreihen und beiden Tastenreihen. Aufgenommen im Spiel selbst
  (Render-Target), für Szene und Zeitalter mit aufgedecktem Umkreis um die Siedlung *(D27)*
- **Abliefern an der Tür (2026-10-06)** — `FollowJob` stellte die Dorfbewohner beim
  Abliefern per `StandCell` neben die Ringkachel, die der Sammelauftrag gewählt hatte: bis
  zu vier Kacheln neben der Tür und bis zu zwei vom Gebäude. Jetzt läuft jeder zu
  `DropOffStand`: die Kachel direkt unter der Tür, die Füße an der Gebäudekante, mehrere
  leicht nebeneinander; ist sie verstellt, die freie Ringkachel, die der Tür am nächsten
  liegt. Die Türmitte ist je Bild gemessen (`DoorCenters`, Stadtzentrum, Mühle, Holzfäller-
  und Bergbaulager je Zeitalter). Wer die Tür nicht erreicht, liefert nicht ab. Gruppe
  `tuer` in `tools/spielablauf`, Abnahme `t1_tueren.py` *(T1)*
- **Wiese aus vier Grassorten (2026-10-06)** — Grundgras, trockenes Gras mit Erdflecken,
  dunkles Gras mit Klee und eine Blumenwiese (Qwen-Image, Gruppe `gras`, im
  handgemalten Stil der Gebäude statt der bisherigen Pixelkunst). `Boden.fx` blendet sie in
  `Meadow` nach Halmhöhe: je Bildpunkt gewinnt die Sorte mit dem größten Gewicht plus
  Helligkeit, an den Grenzen greifen die Halme ineinander. Trockene und satte Gegenden
  folgen großflächigem Rauschen, Blumen wachsen nur in Flecken von zwei bis vier Kacheln,
  am Wald das dunkle Gras. Das Grundgras liegt zweimal, um 90 Grad gedreht, darüber -
  es wiederholt sich nicht sichtbar. Abnahme `g10_wiese.py`, dazu Fotos *(G10)*
- **Die übrigen Gebäude (2026-10-06)** — Kaserne und Palisadenmauer ab der Dunklen Zeit,
  Schießstand, Stall, Schmiede, Markt und Steinmauer ab der Feudalzeit, ein weiteres
  Stadtzentrum, Belagerungswerkstatt, Universität, Kloster und Burg ab der Ritterzeit, das
  Wunder in der Imperialzeit. Kosten laut Spezifikation, Bauzeit, Grundfläche (Mauerstück
  1×1 bis Wunder 5×5) und Werte nach AoE II in `BuildingRules` und `BuildingEntity.Create`
  *(L1)*. Im Spiel eine zweite Tastenreihe (K, P, S, L, E, R, W, Z, X, U, O, C, N) mit
  eigenen Symbolen und Bildern je Zeitalter (Qwen-Image, Gruppen `gebaeude2_*`); nach einem
  Mauerstück bleibt der Setzmodus an, so legt man eine Mauerreihe. Die Kaserne kommt wie in
  AoE II schon in der Dunklen Zeit. Neue Gruppe `neubauten` in `tools/spielablauf` *(L2)*
- **Boden aus einem Guss (2026-10-05)** — Gras, Sand und Wasser zeichnet ein Shader
  (`Content/Effects/Boden.fx`) in einem Durchgang: weiche, unregelmäßige Ufer und Strände
  statt Kachelkanten, nasser Sand am Wasser, Wasser mit Tiefe, Wellen, Glanz, Schaum und
  Fischschwärmen unter der Oberfläche, Gras mit großflächigen Farbschwankungen und dunkler
  unter Bäumen. Wo Figuren oft laufen, entstehen Trampelpfade, die ohne Verkehr wieder
  zuwachsen (`Tile.Wear`, `TileMap.Trample`, `TileMap.RegrowGrass`; Gruppe `pfad` in
  `tools/spielablauf`). Seen liegen nicht mehr in den Startzonen - das Räumen schnitt
  sonst gerade Ufer hinein *(G5)*
- **README-Bilder (2026-10-04)** — Hauptmenü, Spielszene und Karte neu aufgenommen, dazu
  `docs/bilder/zeitalter.jpg`: dieselbe Siedlung in allen vier Zeitaltern. Aufgenommen im
  Fenstermodus - im Vollbild liefert `checks/_fenster.ps1` nur ein eingefrorenes, fast
  schwarzes erstes Bild *(D23)*
- **Baustellen ohne Sicht (2026-10-04)** — eine Baustelle deckte den Nebel sofort mit der
  vollen Sichtweite des fertigen Gebäudes auf, ein Wachturm-Fundament also 10 Kacheln weit
  (Hinweis des Nutzers). Jetzt spendet erst das fertige Gebäude Sicht:
  `BuildingEntity.IsUnderConstruction` in AoE.Core, abgeglichen vor jeder Sichtrechnung
  (`TileMap.UpdateFogOfWarForPlayer`). Zwei Tests in `FogOfWarTests`, dazu prüft die Gruppe
  `turm` in `tools/spielablauf` den halb gebauten Turm *(C7b)*
- **Dorfbewohner je Zeitalter (2026-10-04)** — dieselbe Figur, je Zeitalter umgekleidet:
  Kittel und Strohhut, dann Tunika mit Lederwams, Gürteltasche und Filzkappe, dann
  gepolstertes Wams mit grauer Gugel, zuletzt Wams mit Puffärmeln, weißem Kragen und
  Federbarett; das Blau der Spielerfarbe bleibt das Hauptkleidungsstück. Qwen-Image malte
  über dem Bild der Dunklen Zeit alles neu außer Gesicht, erhobenem Unterarm samt Faust und
  Schuhen (Gruppe `einheiten`, `dorfbewohner_zeit_*`) — so sitzt das Werkzeug ohne neue
  Messung in der Faust. Nur die Faust stehen zu lassen reichte nicht: das Modell malte
  daneben eine zweite, und rechts unter dem Arm eine dritte Hand. Laufbilder je Zeitalter
  wie bisher über die Beine (`einheiten_lauf`). Abnahme `c4h_dorfbewohner.py` *(C4h)*
- **Gebäude je Zeitalter (2026-10-04)** — Stadtzentrum, Haus, Mühle, Holzfäller- und
  Bergbaulager in vier Fassungen, der Wachturm ab der Feudalzeit in drei: Langhaus und
  Hütten aus Holz, Flechtwerk und Stroh, dann Bretter und Schindeln auf Bruchstein, dann
  Burg und Steinbau mit Ziegeldach, zuletzt Haustein mit Schiefer, Kupfer und Gold
  (`tools/bilder/bilder.json`, Gruppen `gebaeude_dunkel` bis `gebaeude_imperial`, Bilder in
  `Content/Gebaeude/<zeitalter>/`). Fahnentuch und Mühlennabe sind je Bild gemessen
  (`FlagCloth`, `MillHubs`); fehlt ein Bild, gilt das des Zeitalters davor. Lange
  Materialbeschreibungen im Prompt kippten Qwen-Image ins Gemalte, die Prompts sind deshalb
  knapp. Für Rot umgefärbt wird hier nur Blau ab Sättigung 0,6 (`blau_saettigung` in
  `uebernehmen.ps1`), sonst färben sich Schieferdächer und Steinschatten mit. Abnahme
  `c4g_zeitalter.py`; `c6g` zählt seither Strohgold statt Ziegelrot *(C4g)*
- **Kartengrößen (2026-10-04)** — im Hauptmenü wählt „Karte“ zwischen Standard (64×64),
  Groß (90×90, fast die doppelte Fläche) und Maximal (128×128, die vierfache); die Wahl
  bleibt gespeichert. Wälder, Steinbrüche, Goldadern, Seen und alle Herden wachsen mit der
  Fläche (`MapSettings.ForSize`), die Minimap bleibt gleich groß. Schnellstart:
  `--rts --karte gross` bzw. `--karte max`. Prüfung: `tools/kartenpruefung -- groessen`,
  `tools/spielablauf -- karten menue` *(C11)*
- **Farm und Tiere (2026-10-04)** — `Felder/weizen.png` zeigt das ganze 3×3-Feld mit
  Weizenreihen und Holzzaun, `Felder/acker.png` dasselbe Feld nach der Ernte: Qwen-Image
  hat dafür nur die Weizenreihen neu gemalt (Rechteckmaske), Furchen und Zaun blieben
  Pixel für Pixel. Jede Kachel zeigt ihren Teil (`FieldPart`), erntet und wächst für sich.
  Schafe und Rehe kommen als freigestellte Sprites (`Tiere/`), je zwei Varianten. Jedes
  Tier trägt ein `WildAnimal` mit eigenem Wandertakt — vorher zog je Art immer nur das
  letzte Tier der Liste —, läuft einen Schritt sichtbar in einer Sekunde
  (`WILD_STEP_SECONDS`) und blickt in Laufrichtung. Neue Gruppe `herde` in
  `tools/spielablauf`
- **Grafik aus dem Bildgenerator (2026-10-03)** — ComfyUI mit Qwen-Image-2512 läuft lokal
  unter `D:\Apps\ComfyUI`; `tools/bilder` erzeugt daraus die Spielgrafik (Prompts und Seeds
  in `bilder.json`, wiederholbar) und bereitet sie für `Content/` auf. Neu: Menübild,
  Symbole der Befehlstasten, Gebäude als Sprites in Spielerfarbe (C6i, C6g), Dorfbewohner
  als Sprite mit Bewegung aus dem Code (C6v), kachelbares Gras und fünf Baumarten (C7g),
  Sand, treibendes Wasser sowie Stein- und Goldhaufen (C7r).
  Das Hauptmenü ist mit der Maus bedienbar und wächst mit dem Fenster (E12), der Zoom
  folgt der Fensterhöhe (C10s). Bilder des Spiels in `docs/bilder/` und im README
- **2026-10-03** — die DX-Fassung startet wieder (E11, Grafikprofil HiDef); die Minimap
  sitzt rechts unten in derselben Draufsicht wie die Spielkarte, ein Klick rückt die
  Kamera (C6m, C6d); die Zoomgrenzen wachsen mit der Fensterhöhe (C10z); die
  Befehlstasten reagieren auf Klicks, Bautasten gibt es nur mit ausgewähltem
  Dorfbewohner, und jede Taste ist so breit wie ihr Name (C8c, C8d)
- **C4 Zeitalter** — Taste A steigt im Stadtzentrum auf: Feudalzeit 500 Nahrung, 130 s;
  Ritterzeit 800 Nahrung + 200 Gold, 160 s; Imperialzeit 1000 Nahrung + 800 Gold, 190 s.
  Solange der Aufstieg läuft, bildet das Stadtzentrum nicht aus. Erstes Gebäude der
  Feudalzeit ist der **Wachturm** (Taste T, 50 Holz + 125 Stein, sieht 10 Kacheln weit).
  Logik als `AgeRules` und `AgeProgress` in AoE.Core (`Economy/Ages.cs`, 46 Tests);
  neue Gruppen `minimap`, `zoom`, `leiste`, `zeitalter` und `turm` in `tools/spielablauf`,
  dazu ein Bildschirmfoto-Test (`checks/c6m_minimap.py` mit `checks/_fenster.ps1`)
- **C1 Dorfbewohner-Loop** — Linksklick auf eine Ressource schickt Dorfbewohner
  sammeln: Traglast 10, Abgabe am Stadtzentrum, selbstständig zurück, bei
  erschöpfter Quelle zur nächsten gleichen. Logik als `GatherJob` in AoE.Core
  (15 Tests), im Spiel über `TileMapGatherWorld`
- **Kartengenerator reichlicher** — jede Karte trägt jetzt mehr: 9–13 Waldklumpen
  (vorher 7–10), 3–6 Steinbrüche und 2–5 Goldminen (je um 25 % gesenkt, weil
  Gold und Stein zu viel generiert wurden), mehr Fische, mehr
  Beerenbüsche. Alle Werte stecken im neuen `MapSettings` — das Menü kann sie
  ändern und daraus direkt eine neue Karte bauen (`TileMap(w,h,t,s)`), ohne die
  Generatoren anzugreifen. `MapSettings.Default` = die reichere Grundausstattung.
- **Schaf-Wanderung** — Schafe laufen frei über die Wiese (ein Schaf pro Kachel,
  100 Nahrung). Reserviert sind sie, während ein Dörfler sie erntet: das Schaf
  bleibt am Platz, die Wanderung überspringt es, und `FindNearestSource` lässt
  es für andere Dorfbewohner wegfallen, damit zwei nicht auf dasselbe Schaf
  laufen. Neue Gruppe `schafe` in `tools/spielablauf` prüft Reservierung und
  Wanderung ohne Grafik.
- **C5f Farm** — Getreidefeld (Taste G, 3×3, 175 Nahrung/Kachel, Regrow 100 s),
  neue `farm`-Gruppe in `tools/spielablauf` als Abnahme
- **C5d, C8b** nach dem Screenshot vom 2026-10-01: Wer ein Gebäude fertig hat und nichts
  zu sammeln bekommt, baut an der nächsten unfertigen eigenen Baustelle im Umkreis von
  8 Kacheln weiter; ein Linksklick auf eine eigene Einheit wählt sie aus, statt sie
  wegzuschicken. Neues Werkzeug `tools/spielablauf` prüft solche Abläufe ohne Grafik
- **C5 Bauen** — Dorfbewohner wählen, Taste H (Haus), M (Mühle), F (Holzfällerlager)
  oder B (Bergbaulager), dann den Bauplatz anklicken: die Baustelle wird bezahlt, die
  gewählten Dorfbewohner gehen hin und bauen. Bauzeiten aus AoE II (Haus 25 s, Mühle und
  Lager 35 s); mehrere Arbeiter mit abnehmendem Ertrag — jeder weitere baut ein Drittel
  so schnell wie der erste, vier brauchen die halbe Zeit. Baustellen zählen weder als
  Wohnraum noch als Abgabestelle; wer ein Lager gebaut hat, sammelt gleich daneben.
  Logik als `BuildingRules` und `Construction` in AoE.Core (`Economy/Construction.cs`,
  36 Tests)
- **C2 Bevölkerungslimit** — die Grenze ergibt sich aus den Gebäuden (Stadtzentrum und
  Haus je 5, höchstens 200). Taste Q bildet im Stadtzentrum einen Dorfbewohner aus
  (25 Nahrung, 25 s); bei erreichter Grenze steht die Ausbildung still, „Bev." wird rot
  und die obere Leiste sagt es. Logik als `Population` und `TrainingQueue` in AoE.Core
  (`Economy/Training.cs`, 22 Tests)
- **Häuser** — mit gewähltem Dorfbewohner Taste H, dann Linksklick: 2×2 Kacheln für
  25 Holz; eine grüne oder rote Fläche zeigt, ob das Haus passt. Seit C5 eine Baustelle
  mit 25 s Bauzeit
- **C1t** ein beladener Dorfbewohner behält seine Traglast, wenn man ihn an eine andere
  Quelle derselben Ressource schickt
- **E10** Stein und Gold waren nie abbaubar: `StampResource` setzte nie die Ressource.
  Ein Prüfprogramm über 20 Karten fand Stein 0 von 557 Kacheln abbaubar, Gold 0 von 235;
  die grauen Deko-Felsen ohne Ressource sind weg
- **C1r** Startrohstoffe: Steinbruch und Goldmine liegen 7–10 Kacheln vom Stadtzentrum
  statt im Schnitt 33
- **C1f** Fischen: Schwärme an der Küste mit 200 Nahrung, gefangen vom Ufer aus
- **G1–G4** Wald als Blätterdach mit überlappenden Kronen, ruhigeres Wasser mit Uferlinie
- **E8, E9** nach dem Screenshot vom 2026-09-30: Auswahl, Auswahlrahmen und
  Lebensbalken beziehen sich auf die ganze Figur; das Stadtzentrum wächst beim
  Zoomen im Ganzen mit
- **Nebel des Krieges (C7n)** — nie Gesehenes schwarz, einmal Gesehenes abgedunkelt,
  fremde Einheiten nur in Sicht; die Sicht wird viermal pro Sekunde gerechnet
- **Maussteuerung (C8a)** — rechts markieren (Klick oder Rahmen), links gedrückt
  halten und ziehen verschiebt die Karte, ein kurzer Linksklick ist der Befehl
- **B6 Gebäude** — `Building.Core` hält eine `BuildingEntity`; Gebäude spenden Sicht,
  das Stadtzentrum 5 Kacheln weit
- **Leerlauf** — Zähler „Untätig" in der unteren Leiste, Taste „." springt zum
  nächsten untätigen Dorfbewohner; eine einzeln gewählte Einheit zeigt LP und Traglast
- **C10** Kantenscrollen; die Pfeiltasten schwenken jetzt in Pfeilrichtung (E6)
- **E5** Startplatz: die Dorfbewohner von Spieler 1 standen im Stadtzentrum
- **E7** die Karte lag eine halbe Kachel neben Maus und Einheiten und wuchs beim Zoomen nicht mit
- Nahrung laut Spezifikation: Schaf 100, Beerenbusch 125

Die Screenshots in `docs/Screenshot 2026-09-23 *.png` zeigen den Stand davor
und haben drei der vier Fehler aufgedeckt. Sie sind als Referenz nützlich.

`docs/Screenshot 2026-09-30 224900.png` bestätigt Nebel, Sammelkreislauf (Nahrung 220,
Holz 300), Startplatz und Kachelraster — und hat E8 und E9 aufgedeckt.
`docs/Screenshot 2026-09-30 230012.png` zeigt den Auswahlrahmen um die ganze Figur (E8).
`docs/Screenshot 2026-10-01 225150.png` bestätigt im Spiel: Stein wird abgebaut
(Stein 210, E10), Steinbruch und Goldmine liegen nah am Stadtzentrum (C1r), Baumkronen
ragen über die Kachelgrenzen (G1, G3), „Bev. 4/5" kommt aus den Gebäuden (C2b), das
Baumenü steht in der unteren Leiste (C5b), und Auswahl und Sammelbefehl wirken
(Untätig: 0). Gebaut und ausgebildet wurde darauf noch nichts.
`docs/Screenshot 2026-10-01 225659.png` bestätigt Ausbildung und Limit („Bev. 5/5" rot,
Hinweis oben), ein fertiges Holzfällerlager mit Sammeln danach und die Baustellen-Grafik.
Zwei Baustellen standen bei rund 5 %: ihre Bauarbeiter waren abgezogen worden — per
Linksklick, der hier ein Befehl ist, oder per neuem Bauauftrag. Die Nachstellung in
`tools/spielablauf` hat das bestätigt; daraus wurden C5d und C8b.

Nächste sinnvolle Schritte, in dieser Reihenfolge:

1. **Sichtprüfung durch den Nutzer** — die neuen Bäume, das feinere Gras und die Leisten je
   Zeitalter (T1b, G11, H1) im Spiel; dazu Gehen (C4l, C4m) und Schwertschlag
   (C4n) sind fertig; in Zeitlupe in `docs/bilder/gang.gif` und `docs/bilder/schlag.gif`. Der
   Bogenschütze schießt beim Angriff noch nicht sichtbar (Bogen spannen, Pfeil) - siehe C4n.
2. **Feuer in der Schmiede animieren** (Hinweis des Nutzers, 2026-10-07) — das Feuer ist
   bisher nur gemalt und steht still. Siehe C6f.
3. **Rechteckige Karte** (Hinweis des Nutzers, 2026-10-07) — die Karte ist quadratisch
   (`TileMap(side, side, …)`), sie sollte ungefähr das Seitenverhältnis des Bildschirms
   haben. Siehe C11r.
4. **Sichtprüfung im Spiel** — offen: Aufstieg in die Feudalzeit und Bau eines Wachturms,
   Bauen von Mühle und Bergbaulager, Fischen, Kantenscrollen, die Hinweise beim Zeigen
   auf eine Befehlstaste und der Klick in die Minimap. C5d, C8b, die Farm, die Leiste und
   die Zeitalter prüft `tools/spielablauf` bereits ohne Grafik. Bestätigt sind Wald, Stein,
   Gold, die Maussteuerung, Ausbildung mit Q, die rote Bevölkerungsanzeige, das
   Holzfällerlager und die Baustellen-Grafik (Screenshots vom 2026-10-01), dazu per
   Bildschirmfoto Menü, Leiste mit Symbolen, Minimap, Gebäude, Dorfbewohner, Gras und
   Wald (2026-10-03).
5. **Restliche Grafik im neuen Stil** — Beerenbüsche, Fische und Baustelle zeichnet noch
   der Code; neben den Sprites wirken sie flach. Echte Laufbilder für die
   Dorfbewohner bräuchten Qwen-Image-Edit (dieselbe Figur in mehreren Posen).
6. **Bedienoberfläche mit der Auflösung skalieren** — Leisten, Schrift, Tasten und Minimap
   haben feste Pixelmaße und wirken in der DX-Fassung (2707 px hoch) halb so groß; dazu
   ein Infokästchen am Mauszeiger statt des Hinweises in der Leistenmitte.
7. **Die neuen Gebäude mit Leben füllen** — seit L1/L2 sind alle Gebäude baubar; Kaserne,
   Schießstand und Stall bilden seit P1 Miliz, Bogenschütze und Späher aus. Offen:
   Einheiten für Belagerungswerkstatt und Burg, die Kampfschleife, der Markt den Handel,
   das Kloster Mönche; Schmiede, Universität, Lager und Stadtzentrum forschen seit P2 (C14).
   Dorfkern, Mauern und Krieger siehe C13.
8. **Kampfschleife** — `Unit.Attack()` existiert seit B2 und wird nirgends aufgerufen;
   Gebäude angreifen geht seit K2 (`BuildingCombat`), Einheiten gegeneinander noch nicht.
   Auf Wunsch des Nutzers zurückgestellt (2026-09-30).
9. **Veraltete Abnahmen** — `c1n`, `c1c` und `c8a` prüfen Schreibweisen von früher (die
   Schaf-Nahrung als Zahl, „Untätig" in `DrawUI`, „Links ziehen" im Hilfetext) und sind
   deshalb rot, obwohl das Verhalten stimmt; auf Verhalten umstellen.
10. **Wild (Rehe)** — fertig: 5–8 Herden je 3–9 Rehe (seither ~70 % der Schaf-Dichte
    statt ~100 %), je Spieler eine Herde 12–20 Kacheln vom Stadtzentrum (C7d), 150 Nahrung,
    reserviert wie die Schafe, solange ein Dorfbewohner jagt (Gruppe `wild` in
    `tools/spielablauf`, Bestand in `tools/kartenpruefung -- wild`); Sprites und Bewegung seit
    C7t. Offen nur die Sichtprüfung, dass zwei Dörfler nicht auf dasselbe Reh laufen.
    Seit C7k dazu **Kaninchen** (4–6 Gruppen je 3–6, 50 Nahrung, hoppeln flink) und
    **Wildschweine** (3–5 Rotten je 1–3, 300 Nahrung, gemächlich) mit Stand-, Lauf- und
    Fleischbildern; noch ohne Gegenwehr, die bräuchte die Kampfschleife.

11. **Gebäude je Zeitalter** — fertig seit C4g (2026-10-04): je Gebäude ein Bildsatz pro
    Zeitalter aus Qwen-Image, gewählt nach dem Zeitalter des Besitzers. Offen nur die
    Sichtprüfung im echten Aufstieg und im Rot des zweiten Spielers. Die Vorgabe des
    Nutzers, nach der die Bilder entstanden sind:
    - **Dunkle Zeit:** Die Siedlungen wirken wie provisorische Lager oder ärmliche,
      frühmittelalterliche Dörfer. Aussehen: klein, flach, asymmetrisch, meist unebene Formen;
      noch keine befestigten Strassen oder Fundamente. Materialien: fast ausschliesslich
      Holzstämme, Lehm, Flechtwerk und einfache Strohdächer.
    - **Feudalzeit:** Die Siedlung verwandelt sich in ein organisiertes, handwerklich
      entwickeltes Dorf. Aussehen: rechteckiger, stabiler und höher; erste kleine Holztürme
      und Palisadenwälle. Materialien: weiterhin primär Holz, aber deutlich sauberer
      verarbeitet (z. B. gehobelte Bretter); Dächer oft aus Holzschindeln oder dickerem Reet,
      erste Fundamente aus Bruchstein oder Lehmziegeln.
    - **Ritterzeit:** Das Stadtbild wandelt sich radikal in eine wehrhafte, hochmittelalterliche
      Festung. Aussehen: deutlich grösser, oft mehrere Stockwerke, massiv; mächtige Burgen,
      dicke Mauern und befestigte Stadttore. Materialien: grauer oder sandfarbener Stein
      (Mauerwerk); Dächer vermehrt mit roten oder blauen Tonziegeln, Holz nur noch für
      Dachstühle, Stege oder sekundäre Bauteile.
    - **Imperialzeit:** Die Gebäude erreichen die Stufe einer prachtvollen, spätmittelalterlichen
      oder frühneuzeitlichen Metropole. Aussehen: elegant und repräsentativ; Zierelemente, hohe
      Bögen, filigrane Fensterkonstruktionen und monumentale Ausmasse (wie das Weltwunder).
      Materialien: hochwertiger, feiner Haustein und Marmor; Dächer in kräftigen Farben
      (Schiefer- oder Kupferstrukturen), Metallelemente, Flaggen und edle Verzierungen.

Farm (C5f) ist fertig: Dorfbewohner wählen, Taste G (Getreide) drücken, Bauplatz
anklicken — 3×3-Feld von 175 Nahrung. Die ausgewählten Dorfbewohner ernten die
Getreidekacheln direkt wie jede andere Nahrungsquelle (Traglast bleibt erhalten) und
liefern an Mühle oder Stadtzentrum; nach der Ernte wächst jede Kachel in rund
100 s wieder nach. Die Grafik wechselt von hellem Getreide zu dunkler Erde, und
`tools/spielablauf` prüft das ohne Grafik. Siehe Block C, C5f.

Werkzeuge: `tools/ollama-agent/` verteilt `[L]`- und `[R]`-Punkte an ein lokales
Ollama-Modell (`qwen3.8:27b`, 256K Kontext) und nimmt sie per Build, Test und
Prüfskript aus `tools/ollama-agent/checks/` ab (`py run_tasks.py --list`).
`[X]`-Punkte gehören nicht dorthin — bei C1, C2 und C5 kamen Entwurf und Tests von
Claude, die Implementierung von `GatherJob`, `Population`, `TrainingQueue` und
`Construction` vom lokalen Modell.
Die 256K stehen seit 2026-10-01 als `num_ctx` im Modell selbst; das unveränderte
Original liegt als `qwen3.8:27b-orig` daneben.

Spielgrafik: `py tools/bilder/qwen_image.py <gruppe>` erzeugt Bilder über ComfyUI (Server
starten mit `D:\Apps\ComfyUI\ComfyUI_windows_portable\run_nvidia_gpu.bat`; Modelle
Qwen-Image-2512 fp8, Qwen2.5-VL-7B als Text-Encoder, BiRefNet zum Freistellen), und
`pwsh -File tools/bilder/uebernehmen.ps1` übernimmt die in `bilder.json` gewählten Seeds
nach `Content/` (zuschneiden, Spielerfarben Blau/Rot, kachelbar). ComfyUI und das
Ollama-Modell passen nicht zugleich in die 32 GB der Grafikkarte: vor einem Agent-Lauf
`POST /free` an ComfyUI, vor Bildern `ollama stop qwen3.8:27b`. Fehlt eine Grafik,
zeichnet das Spiel wie früher selbst.

Kartenfehler sehen die Unit-Tests nicht – sie kennen das Spielprojekt nicht. Dafür gibt
es `tools/kartenpruefung`: `dotnet run --project tools/kartenpruefung -- rohstoffe start fisch`
erzeugt 50 Karten und prüft Regeln des Kartengenerators; bei einem Verstoß endet es mit
Fehlercode und taugt so als Abnahme.

Ähnlich für den Spielbildschirm: `dotnet run --project tools/spielablauf -- bauen weiterbauen linksklick`
lässt die Spielschleife ohne Grafik auf echten Karten laufen und prüft, wer baut, wer
stehen bleibt und was ein Klick bewirkt. Den Bildschirm erreicht es per Reflection —
benennt jemand eine Methode um, meldet es das, statt still falsch zu prüfen.

## Verifizierter Ist-Zustand

| Projekt | Framework | Build | Bemerkung |
|---|---|---|---|
| `src/AoE.Core` | net10.0 | OK, 0 Fehler | Reine Logik, keine MonoGame-Abhängigkeit |
| `tests/AoE.Tests` | net10.0 | **267/267 grün** | Einheiten und Ressourcen (15), Sammelauftrag (21), Ausbildung und Bevölkerung (22), Bauen (74), Zeitalter (46), Forschungen (43), Konter-Dreieck (5), Gebäudekampf (9), Wegfindung (3), Nebel (8), Wirtschafts-KI (20), Fehlersuche (1) |
| `demo/DemoApp` | net10.0 | OK | referenziert AoE.Core |
| `AgeOfEvolutions.Core` | net10.0 | OK, 0 Fehler | Dateiname seit H2 kanonisch |
| `AgeOfEvolutions.DesktopGL` | net10.0 | OK, 0 Fehler / 0 Warnungen | startbar |
| `AgeOfEvolutions.WindowsDX` | net10.0-windows | OK, 0 Fehler | nur Windows |

**Die .NET-10-Migration und der Build sind erledigt.** Der eigentliche Engpass ist ein
anderer, siehe Block B.

## Zielplattformen

**PC und Mac.** `DesktopGL` ist der plattformübergreifende Pfad (Windows, macOS, Linux) und
deklariert `win-x64;osx-x64;osx-arm64;linux-x64` — `osx-arm64` ist für Apple Silicon zwingend.
`WindowsDX` bleibt als zusätzliche reine Windows-Variante.

**Mobile ist kein Ziel.** Die Android- und iOS-Projekte der MonoGame-Vorlage wurden am
2026-09-23 gelöscht; das deckt sich mit `game_plan.md` („Windows, macOS, Linux only — no
mobile"). Die 84 Dateien sind entfernt, die Löschung ist mit `4cb57d4` committet.

## Eignung für Delegation

- **[L] lokal delegierbar** — mechanisch, eng umrissen, durch Build/Test abnehmbar
- **[R] lokal mit Review** — eine Datei, klares Ziel, Ergebnis muss gesichtet werden
- **[X] nicht delegieren** — schneidet quer durch die Architektur, braucht Entwurfsentscheidungen

**Abnahmekriterium für jeden Punkt**, sofern nicht anders genannt:

```
dotnet build AgeOfEvolutions/AgeOfEvolutions.DesktopGL/AgeOfEvolutions.DesktopGL.csproj
dotnet test  tests/AoE.Tests/AoE.Tests.csproj
```

beide fehlerfrei.

---

# Block A — Hygiene & Build (blockiert alles andere)

### H1 · `.gitignore` anlegen, Build-Artefakte aus dem Index nehmen [L]

Die `.gitignore` existiert, wirkt aber nicht auf Dateien, die bereits getrackt sind:
485 Dateien unter `bin/` und `obj/` sowie 16 unter `.vs/` liegen weiterhin im Index.
Deshalb meldet `git status` nach jedem Build geänderte `.dll`-, `.pdb`- und
`.cache`-Dateien (am 2026-09-30: 32 Stück).

- [x] `.gitignore` für .NET + MonoGame + Visual Studio anlegen *(Agent H1)*
- [ ] `git rm -r --cached` für `bin/`, `obj/`, `.vs/`, `Content/obj/`, `Content/bin/`
      — **bewusst nicht automatisiert.** Das stellt 501 Löschungen in den Index;
      dieser Schritt gehört dir, nicht einem Agent. Per Trockenlauf geprüft, erfasst
      genau diese 501 Dateien und keine Quelldatei, in PowerShell wie in der Git-Bash:
      `git rm -r --cached -- ':(glob)**/bin/**' ':(glob)**/obj/**' ':(glob)**/.vs/**'`
      Der früher hier notierte Befehl mit `AgeOfEvolutions/*/bin` erfasste `src/`,
      `tests/`, `demo/` und `Content/` nicht und bricht in der Git-Bash mit
      „pathspec did not match" ganz ab.
- [ ] Abnahme: `git status --short` zeigt nur noch echte Quelldateien

### H2 · csproj-Dateinamen begradigen [L]

Die Datei hieß auf der Platte `AgeofempiresClone.Core.csproj`, während Git und alle
verweisenden Projekte die kanonische Schreibweise nutzten. Unter Windows fiel das nicht auf —
**auf Linux und macOS hätte es den Build gebrochen**, und genau die sind jetzt Zielplattform.

- [x] Auf die kanonische Schreibweise `AgeOfEvolutions.Core.csproj` umbenannt *(Agent H2)*

### H3 · Doppelten `ProjectReference` entfernen [L]

`AgeOfEvolutions.Core.csproj` verwies **zweimal** auf AoE.Core — zwei ItemGroups,
einmal mit `/`, einmal mit `\`.

- [x] Eine der beiden ItemGroups löschen *(Agent H3 — Build grün)*

### H4 · Solution vervollständigen [R]

Die alte `AgeOfEvolutions.sln` enthielt nur Core + DesktopGL; `src/`, `tests/` und `demo/`
lagen in gar keiner Solution.

- [x] Root-Solution `AgeOfEmpire.slnx` mit allen sechs Projekten *(Agent H4)* — XML-Format
      von .NET 10, keine GUIDs; `dotnet restore AgeOfEmpire.slnx` läuft durch
- [ ] Offen: die alte `AgeOfEvolutions/AgeOfEvolutions.sln` liegt noch neben der `AgeOfEmpire.slnx` und kennt nur Core und DesktopGL — löschen oder bewusst behalten

### H5 · Platformer-Reste entfernen [R]

Aus dem MonoGame-Beispiel übrig, vom Menü nicht mehr erreichbar
(`MainMenuScreen.cs:99` führt korrekt zu `RTSGameplayScreen`):
`Game/Level.cs`, `Game/Player.cs`, `Game/Gem.cs`, `Game/GemState.cs`, `Game/Enemy.cs`,
`Screens/GameplayScreen.cs`, `Content/Levels/`

- [x] 6 Quelldateien + 7 `Content/Levels/*.txt` gelöscht, `Levels/`-Einträge aus der
      `.mgcb` entfernt *(Agent H5)* — `Data/Player.cs` und `Data/Tile.cs` blieben unangetastet

### H6 · Android/iOS auf net10 ziehen [L] — erledigt, danach gegenstandslos

- [x] Auf `net10.0-android` / `net10.0-ios` gezogen *(Agent H6)*
- [x] Projekte anschließend gelöscht, weil Mobile kein Ziel ist — siehe Zielplattformen oben

### H7 · DesktopGL für PC und Mac veröffentlichbar machen [L]

- [x] `RuntimeIdentifiers` = `win-x64;osx-x64;osx-arm64;linux-x64` ergänzt *(Agent H7)*
- [x] Abnahme: `dotnet restore -r osx-arm64` läuft durch
- [x] `dotnet build AgeOfEvolutions.slnx` auf macOS: brach mit NETSDK1100 an `WindowsDX`
      (`net10.0-windows`) ab. `WindowsDX.csproj` setzt jetzt außerhalb von Windows
      `EnableWindowsTargeting`; unter Windows ändert sich nichts *(2026-10-04)*
- [ ] Offen: auf echter macOS-Hardware spielen — auf Apple Silicon (osx-arm64) startet
      `--rts` und läuft ohne Ausnahme, das Spielen selbst ist noch nicht geprüft *(2026-10-04)*

---

# Block E — Fehler aus dem Spielbetrieb

E1–E4 gefunden am 2026-09-23 beim Ausprobieren des laufenden Spiels, E5 bis E7 am
2026-09-30 bei der Code-Durchsicht, E8 und E9 am selben Tag im Screenshot, E10 durch
ein Kartenprüfprogramm — keiner davon durch Tests. Alle behoben.

### E1 · ESC führte in ein schwarzes Nichts [L] — behoben

`RTSGameplayScreen.HandleInput` rief bei ESC nur `ExitScreen()` auf. Das Spiel
wird aber über `LoadingScreen.Load(...)` gestartet, und das beendet **alle**
vorhandenen Screens — das Hauptmenü existierte danach nicht mehr. Der Stack war
nach dem Schließen leer: kein Bild, kein Rückweg.

- [x] Stattdessen `ScreenManager.AddScreen(new PauseScreen(), ...)`.
      `PauseScreen` gab es bereits und führt über „Beenden" sauber ins Hauptmenü.

### E2 · Fast die halbe Karte war Wald [L] — behoben

In `GetRandomTileType` lief `x * 73856093` ab **x ≥ 30** über `int.MaxValue` und
wurde negativ. In C# liefert `%` bei negativem Dividenden ein negatives Ergebnis,
womit `rand < 4` (Wald) immer zutraf.

Nachgerechnet über die ganze 64×64-Karte:

| | vorher | nachher | beabsichtigt |
|---|---|---|---|
| Wald | **47,8 %** | 4,8 % | ~4 % |
| Fels | 2,0 % | 3,5 % | ~3 % |
| Gras | 50,2 % | 91,7 % | ~93 % |

Die Spalten x = 30…58 waren zu **100 %** Wald — eine massive Waldwand über 45 %
der Kartenbreite. Gut im zweiten Screenshot zu sehen.

- [x] `unchecked` plus Ausmaskieren des Vorzeichenbits (`& 0x7FFFFFFF`);
      die Karte bleibt dabei reproduzierbar

### E3 · Kamera zeigte überwiegend Fläche außerhalb der Karte [L] — behoben

Siehe C10. Der erste Screenshot zeigt es deutlich: die Karte klebte im unteren
rechten Viertel, der Rest war leerer Hintergrund.

- [x] `ClampCamera()` in `RTSGameplayScreen`

### E4 · Fenster fest auf 1280×768 [L] — behoben

Auf einer hochauflösenden Anzeige winzig.

- [x] Fenstergröße auf 80 % der Bildschirmfläche, nach unten auf 1280×768 begrenzt
- [x] `Window.AllowUserResizing = true` plus `ClientSizeChanged`-Behandlung, die
      den Bildpuffer nachzieht (mit Wiedereintrittsschutz — `ApplyChanges()` löst
      das Ereignis selbst wieder aus)

### E5 · Dorfbewohner im Stadtzentrum, Startplatz im See [L] — behoben

Die vier Dorfbewohner von Spieler 1 entstanden *innerhalb* des Stadtzentrums
(4 × 4 Kacheln, nicht begehbar). Der auf der inneren Kachel hatte nur gesperrte
Nachbarn — die Wegsuche kennt vier Richtungen — und kam nie heraus. Außerdem
konnten Seen und verstreute Wald- oder Felskacheln auf dem Startplatz landen.

- [x] Dorfbewohner von Spieler 1 rechts unterhalb des Stadtzentrums, spiegelbildlich
      zu Spieler 2 *(Agent E5)*
- [x] `ClearStartArea` räumt Stadtzentrum plus zwei Kacheln Rand zu Wiese frei *(Agent E5)*

### E6 · Pfeiltasten verkehrt herum [L] — behoben

Links schob den Ausschnitt nach rechts, und die Geschwindigkeit hing an der Bildrate
(feste 10 Einheiten pro Frame).

- [x] Richtung korrigiert, 800 Bildschirmpixel pro Sekunde unabhängig von Bildrate
      und Zoom — zusammen mit dem Kantenscrollen *(Agent C10)*

### E7 · Karte um eine halbe Kachel versetzt, ohne Zoom gezeichnet [R] — behoben

`DrawTileMap` setzte die linke obere Ecke jeder Kachel auf ihren *Mittelpunkt*
(`GridToWorld`) und zeichnete sie fest 32 × 32 Pixel groß. Die Karte lag damit
eine halbe Kachel rechts unterhalb der Welt, in der Maus und Einheiten rechnen —
ein Klick auf die rechte untere Hälfte einer Goldmine traf die Nachbarkachel.
Bei Zoom 2 klafften Lücken zwischen den Kacheln, bei 0,5 überlappten sie.

- [x] `TileScreenRect` rechnet das Bildschirmrechteck aus den Weltecken; Kacheln,
      Nahrungsobjekte und die Grundfläche der Gebäude nutzen es *(Agent E7)*

### E8 · Auswahl traf nur die Füße [R] — behoben

Trefferfläche und Auswahlrahmen waren ein Kästchen um die *Füße* einer Einheit, die
Figur reicht aber 24 Einheiten nach oben. Ein Klick auf Kopf oder Oberkörper wählte
nichts aus, der Rahmen umfasste die untere Körperhälfte und das Schaf darunter, der
Lebensbalken lag quer über der Brust (Screenshot vom 2026-09-30).

- [x] `UnitWorldRect` beschreibt die Figur; Klick- und Rahmenauswahl prüfen dagegen,
      Rahmen und Lebensbalken sitzen an der Figur und wachsen mit dem Zoom *(Agent E8)*

### E9 · Stadtzentrum wuchs beim Zoomen nur als Grundplatte [R] — behoben

Haus, Dach, Tür, Fenster und Ecktürme hatten feste Pixelmaße. Bei Zoom 2 blieb das
Haus ein kleiner Fleck im großen Erdplatz. Der Pfad vor der Tür wurde nie gezeichnet —
seine Höhe kam negativ heraus.

- [x] `BuildingPart` rechnet den 128 × 128-Entwurf auf die tatsächliche Größe um *(Agent E9)*

### E10 · Stein und Gold waren nie abbaubar [L] — behoben

`StampResource` setzte bei den Rohstoffklumpen Kacheltyp und Menge, aber nie
`ResourceType`. Die Ressource einer Kachel stammte damit aus ihrem Konstruktor: Wiese
keine, zufälliger Einzelbaum Holz. Ein Prüfprogramm über 20 erzeugte Karten fand Stein
0 von 557 Kacheln abbaubar, Gold 0 von 235, Wald aus Klumpen zu einem Drittel nicht —
und 35 Stein- und Goldkacheln, die Holz lieferten. Dazu kamen rund 3 % graue
Deko-Felsen, die wie Stein aussahen, aber nie etwas hergaben.

- [x] `StampResource` setzt die Ressource des Klumpens *(Agent E10)*
- [x] Deko-Felsen entfernt *(Agent E10)*
- [x] Abnahme: `tools/kartenpruefung` — alle Stein-, Gold- und Waldkacheln abbaubar

### E11 · DX-Fassung stürzte beim Start ab [L] — behoben

`CreateSwapChain` meldete `E_INVALIDARG`: Mit dem Standardprofil Reach legt MonoGame unter
DirectX ein Gerät mit Feature Level 9_3 an, dessen Bildpuffer höchstens 4096 Pixel breit sein
darf. Mit der Windows-Einstellung „Hohe DPI-Skalierung überschreiben: Anwendung" rechnet die
DX-Fassung in physischen Pixeln, und 80 % des 6K-Bildschirms sind 4812.

- [x] Grafikprofil HiDef (Feature Level 11_0, bis 16384 Pixel) *(Agent E11)*; die Abnahme
      `checks/e11_dx_start.py` startet die DX-Fassung mit `__COMPAT_LAYER=HIGHDPIAWARE`

### E12 · Hauptmenü nur mit Enter bedienbar [R] — behoben

Screenshot `docs/Menu noch Fehlerhaft.png`: `MainMenuScreen` wertete nur die Tastatur aus,
und Titel und Einträge standen in festen Pixeln — bei hoher Auflösung ein kleiner Fleck oben
in der Mitte, über einer Hintergrundgrafik mit eigenen, nur aufgemalten Schaltflächen.

- [x] Maus: Zeigen wählt, Klick startet; Tafel in der Bildmitte, für 1080 px entworfen und
      mit der Fensterhöhe skaliert, Hintergrund abgedunkelt; eigene Menüschrift (40 pt);
      „Spiel laden" grau, bis es das gibt *(Agent E12)*
- [x] Einstellungen und Pause (`MenuScreen`): die Klickfläche lag eine halbe Zeile unter
      dem Text *(Agent E12)*
- [x] Neue Hintergrundgrafik, erzeugt mit Qwen-Image (`tools/bilder`) — die alte enthielt
      fremde Logos (Age of Empires IV, Xbox Game Studios, Relic, MSN Games) *(Agent C6i)*

### Kein Fehler, nur zur Beruhigung

Die grünen Textfragmente am linken Bildrand der ersten beiden Screenshots waren
der **Visual-Studio-Editor hinter dem Spielfenster** — die sichtbare Zeile war
ein XML-Kommentar aus `Unit.cs` („…mindestens 1."). Kein Renderfehler.

---

# Block B — AoE.Core tatsächlich verwenden (der eigentliche Engpass) [X]

**Erledigt am 2026-09-23.** Das Spiel verwendet jetzt durchgehend `AoE.Core`;
die parallelen Implementierungen sind entfernt. Vorher lief in beiden Projekten
dasselbe Konzept doppelt — und die getestete Variante war die, die im Spiel
*nicht* lief:

| Konzept | vorher im Spiel | jetzt |
|---|---|---|
| Ressourcen | `Resource`-struct + `Dictionary` | `AoE.Core.Economy.ResourcePool` |
| Einheitenwerte | eigene Werttabelle | `UnitEntity` über `Data/CoreUnits.cs` |
| Schaden | `AttackPower` + Faktor 2 | `DamageCalculator` mit Angriffs-/Rüstungsklassen |
| Zustand | eigene `UnitState`-Enum | `AoE.Core.Entities.UnitState` |
| Wegfindung | Luftlinie | `AoE.Core.Pathfinding` (A*) |
| Sicht | `FogOfWar` (ungenutzt) | `AoE.Core.Map.VisibilitySystem` |

Gelöscht: `Data/Resource.cs`, `Data/FogOfWar.cs` (172 Zeilen), die Werttabelle in
`Data/Unit.cs` und die doppelte `UnitState`-Enum.

**Der Entwurf:** das Spiel *hält eine Referenz* auf die Core-Einheit
(`Unit.Core`) und reicht Kampfwerte, Lebenspunkte und Zustand durch. Rendering-State
(`Vector2 Position`, `IsSelected`, `Path`) bleibt im Spiel — Core hat bewusst keine
MonoGame-Abhängigkeit.

### B1 · `ResourcePool` verdrahten [X]

Kleinster Einstieg, weil der Typ isoliert ist und Tests hat.

- [x] `Player.Resources` → `AoE.Core.Economy.ResourcePool`
- [x] `Data/Resource.cs` entfernt — der struct war toter Code, genutzt wurde nur
      die verschachtelte `Type`-Enum (42 Stellen), jetzt `AoE.Core.Entities.Resource`
- [x] Ressourcenanzeige liest aus dem Pool
- [ ] **Verhaltensänderung:** Startkapital kommt jetzt aus `ResourcePool`
      (200/200/100/**200**). Das Spiel begann vorher mit 100 Stein. Falls du die
      alten 100 willst, muss `ResourcePool.START_STONE` geändert werden.

### B2 · `DamageCalculator` verdrahten [X]

- [x] `Unit.DamageAgainst()` und `Unit.Attack()` rechnen über `DamageCalculator`
- [x] Konter-Dreieck steckt jetzt in den Klassenboni statt im pauschalen Faktor 2
- [x] Abnahme: `tests/AoE.Tests/CounterTriangleTests.cs`, 5 Tests
- [x] Nebenbefund behoben: die Werttabelle deckte nur 8 der 17 Einheitentypen ab —
      die übrigen 9 kamen mit **0 Lebenspunkten** zur Welt. `CoreUnits.Create`
      bildet jetzt alle 17 ab.
- [ ] Offen: das Spiel ruft `Attack()` noch nirgends auf — es gibt keine
      Kampfschleife. Das gehört zu Block C.

### B3 · Einheitentypen vereinen [X]

Die größte Einzeländerung — hier zuerst einen Entwurf, dann Code.

- [x] Entschieden: **Referenz statt Vererbung** — `Unit.Core` hält die Core-Einheit,
      `Health`, `MaxHealth`, `AttackPower`, `Armor`, `AttackRange`, `MovementSpeed`
      und `State` werden durchgereicht
- [x] Doppelte `UnitState`-Enum entfernt; `Returning` und `Dead` sind dafür nach
      `AoE.Core` gewandert, weil das Spiel sie braucht und sie dort hingehören

### B4 · A* statt Luftlinie [X]

- [x] Luftlinie durch A* aus `AoE.Core.Pathfinding` ersetzt
- [x] `TileMap` spiegelt die Begehbarkeit in ein `MapGrid`; es wird nur
      aufgefrischt, wenn ein Gebäude gesetzt wurde (`InvalidateNavigation()`)
- [x] Abnahme: `tests/AoE.Tests/PathfindingTests.cs` — läuft um eine Mauer herum,
      und ein eingemauertes Ziel liefert sauber *keinen* Weg statt eines Absturzes

### B5 · Fog of War vereinen [X]

- [x] `Data/FogOfWar.cs` gelöscht, `AoE.Core.Map.VisibilitySystem` verdrahtet
- [x] **Fehler in AoE.Core gefunden und behoben:** `UpdateTileVisibility` setzte
      Kacheln nur auf `Visible` und stufte nie zurück. Einmal Gesehenes wäre für
      immer hell geblieben — der Zustand „erforscht, aber veraltet" kam damit
      *nie* vor. Aufgefallen ist es erst durch den Test.
- [x] Abnahme: `tests/AoE.Tests/FogOfWarTests.cs`, 3 Tests
- [x] Seit C7n aufgerufen: `RTSGameplayScreen` rechnet die Sicht viermal pro Sekunde
      und zeichnet den Nebel *(Agent C7n)*

### B6 · Gebäude auf `AoE.Core.Entities.Buildings` umstellen [X] — erledigt

Beim Aufräumen von Block B übrig geblieben: die `Building`-Klasse in
`TileMap.cs` führt eigene Werte (`MaxHealth = 1000`), während `AoE.Core`
mit `Buildings.cs` (419 Zeilen) und `BuildingEntity` eine eigene Fassung hat.
Dasselbe Muster wie B1–B5, nur für Gebäude.

- [x] `Building` auf `BuildingEntity` umstellen, analog zu `Unit.Core` — `Building.Core`,
      erzeugt von `CoreBuildings.Create`; die Lebenspunkte kommen von dort
- [x] `MapGrid.AddBuilding` nutzen, damit Gebäude Sicht und Wegfindung beeinflussen —
      `TileMap.AddBuilding` meldet das Gebäude an, `VisibilitySystem` zählt Gebäude
      als Sichtquelle (3 neue Tests); Gebäudekacheln sperrte die Wegfindung schon
      über `TileMap.IsWalkable`

---

# Block C — Gameplay (Spec-Reihenfolge, `docs/AgeOfEmpires.md` Kap. „Relevanz")

Die Spec priorisiert nach Spielgefühl, nicht nach Aufwand. C1 ist der Punkt, ohne den sich
nichts nach AoE anfühlt.

### C1 · Dorfbewohner-Loop [X] — höchste Priorität

laufen → sammeln → Traglast 10 → Abgabestelle → zurück → **automatisch fortsetzen**

- [x] Automatische Ressourcensuche und -sammlung — Linksklick auf eine Ressource (seit C8a);
      ist die Quelle erschöpft, sucht der Dorfbewohner im Umkreis von 8 Kacheln
      die nächste gleiche
- [x] Abgabe an nächstgelegene Stelle — Stadtzentrum, seit C5 auch Mühle,
      Holzfällerlager und Bergbaulager, sobald sie fertig gebaut sind
- [x] Automatischer Neustart derselben Ressource nach Abgabe
- [x] Leerlauf-Erkennung: untätig heißt Zustand Idle ohne Sammelauftrag; Zähler in
      der unteren Leiste, Taste „." *(Agent C1c)*
- [x] Startrohstoffe: je Spieler ein Steinbruch und eine Goldmine 7–10 Kacheln vom
      Stadtzentrum, zur Kartenmitte hin *(Agent C1r)*
- [x] Fischen: Schwärme an der Küste, 200 Nahrung; der Dorfbewohner stellt sich auf
      eine begehbare Nachbarkachel und fängt vom Ufer aus *(Agent C1f)*
- [ ] Offen: Sichtprüfung im laufenden Spiel — Holz, Stein und Nahrung (Schafe) sind
      auf den Screenshots vom 2026-09-30 und 2026-10-01 bestätigt, Fischen fehlt noch
- [x] Schickt man einen beladenen Dorfbewohner an eine andere Quelle derselben
      Ressource, behält er seine Traglast — wie in AoE *(Agent C1t)*

Aufbau: die Logik ist ein Zustandsautomat `GatherJob` in AoE.Core
(`Economy/GatherJob.cs`, 15 Tests); das Spiel übersetzt ihn über
`Data/TileMapGatherWorld.cs` auf die Kachelkarte. Vorher war Sammeln im Spiel
unerreichbar: kein Befehl setzte den Zustand `Gathering`, `Returning` hatte keine
Behandlung, alle Einheiten teilten sich einen Sammel-Timer, und abgeliefert wurden
nur 70 %.

### C2 · Bevölkerungslimit [R] — erledigt

- [x] Gemeinsame Obergrenze (default 200) für Wirtschaft **und** Armee — `Population.Capacity`
      zählt die Gebäude, jede Einheit belegt einen Platz *(Agent C2a, C2b)*
- [x] Haus = +5 Plätze, Kosten 25 Holz — Taste H, Linksklick, 2×2 Kacheln; seit C5 eine
      Baustelle mit 25 s Bauzeit, erst das fertige Haus zählt *(Agent C2c, C5b)*
- [x] Bei vollem Limit Produktion stoppen — mit sichtbarem UI-Hinweis: „Bev." rot, oben
      „Bevölkerungslimit erreicht" *(Agent C2b)*
- [x] Ausbildung im Stadtzentrum: Taste Q reiht einen Dorfbewohner ein (25 Nahrung, 25 s,
      bis zu 15 in der Warteschlange) *(Agent C2b)*

Achtung: die Spezifikation nennt 25 Nahrung je Dorfbewohner, im Original kostet er 50.
Übernommen ist der Wert der Spezifikation (`VillagerCost` in `RTSGameplayScreen`).

### C3 · Endliche Vorkommen [L]

Laut Spec Punkt 6: ohne das hat eine Partie kein natürliches Ende.

- [x] Abbau reduziert den Vorrat (`tile.ResourceAmount--`) statt zu regenerieren *(Agent C3)*
- [x] Erschöpfte Quelle verschwindet bzw. wird zu Normalgelände *(Agent C3)*
- [x] Folge davon geschlossen durch C1: der Dorfbewohner sucht die nächste gleiche
      Quelle, die erschöpfte Kachel wird zu Wiese

### C4 · Zeitalter als Gate [X]

- [x] Stadtzentrum: Upgrade nach Feudal-/Ritter-/Imperialzeit — Taste A oder „Zeit." in der
      Leiste; solange der Aufstieg läuft, bildet das Stadtzentrum nicht aus *(Agent C4b)*
- [x] Kosten 500N · 800N+200G · 1000N+800G, Dauer 130 / 160 / 190 s — `AgeRules` und
      `AgeProgress` in AoE.Core (`Economy/Ages.cs`, 46 Tests) *(Agent C4a)*
- [ ] **Gold für die Imperialzeit auf 300 senken** (Wunsch 2026-10-04): Kosten werden
      1000N + **300G** statt 1000N + 800G — `Economy/Ages.cs`, die dazu gehörigen Tests und
      die `zeitalter`-Gruppe in `tools/spielablauf` umstellen
- [ ] Jedes Zeitalter schaltet Gebäude und Einheiten frei — die Regel steht
      (`AgeRules.RequiredAgeOf`, Baumenü und Tasten richten sich danach). Erstes
      Gebäude der Feudalzeit: **Wachturm** (Taste T, 50 Holz + 125 Stein, 80 s, sieht
      10 Kacheln weit) *(Agent C4c)*. Offen: weitere Gebäude und die Einheiten — die
      brauchen die zurückgestellte Kampfschleife
- [x] Gebäude sehen in jedem Zeitalter anders aus: ein Bildsatz je Zeitalter unter
      `Content/Gebaeude/<zeitalter>/`, gezeichnet im Zeitalter des Besitzers *(Agent C4g)*
- [x] Dorfbewohner kleiden sich nach dem Zeitalter ihres Besitzers: Stand- und Laufbilder je
      Zeitalter unter `Content/Einheiten/<zeitalter>/` *(Agent C4h)*
- [x] **C4l Laufbilder der Imperialzeit** (Hinweis des Nutzers, 2026-10-07): die
      Dorfbewohner der Imperialzeit laufen merkwürdig und brauchen neue Laufbilder.
      Genauer (2026-10-09): beim Gehen sind die Beine nicht mit dem Körper verbunden.
      Ausdrücklicher Wunsch: hier wirklich den High-Def-Builder verwenden.
      `Content/Einheiten/imperial/dorfbewohner_lauf1_*` und `_lauf2_*` neu erzeugen (Gruppe
      `einheiten_lauf`, wie bei C7v nur die Beine neu malen, Rumpf und Faust bleiben). Mit
      dem Standbild abgleichen: gleicher Zuschnitt (Feld „figur“ in `uebernehmen.ps1`), Füße
      auf derselben Höhe, keine Puffärmel oder Kragen, die zwischen den Bildern springen.
      Abnahme: Sichtprüfung im Spiel neben den Laufbildern der übrigen Zeitalter, dazu
      `c4h_dorfbewohner.py` *(erledigt 2026-10-09: Hose und Stiefel neu gemalt, Gehphasen aus C4m)*
- [x] **C4m Natürliche Gehbewegung für alle Figuren** (Wunsch des Nutzers, 2026-10-09): alle
      Beinbewegungen kontrollieren, hier mit maximalem Aufwand, damit Soldaten und
      Dorfbewohner natürlich gehen. Umfasst C4l. Betroffen: Dorfbewohner in allen vier
      Zeitaltern, Miliz, Bogenschütze und Späher samt Pferd (Gangart der vier Pferdebeine).
      Bisher besteht ein Schritt aus nur vier Phasen (`lauf1`, Stand, `lauf2`, Stand in
      `_villagerWalk` und `_unitWalk`); das wirkt hölzern. Zu prüfen und zu erneuern:
      - mehr Phasen je Doppelschritt (Aufsetzen, Abfedern, Durchschwingen, Abdruck je Bein),
        die Beine in jeder Phase sichtbar an der Hüfte, Füße auf derselben Bodenlinie,
        gegengleich schwingende Arme, leichtes Auf und Ab des Rumpfs
      - Schrittlänge passend zur Laufgeschwindigkeit, damit die Füße nicht über den Boden
        rutschen (`VillagerWalkPhase(motion.Walked)` taktet nach der gelaufenen Strecke)
      - gleicher Zuschnitt, gleiche Kleidung und Waffe in allen Phasen, nichts springt
      Erzeugen mit dem High-Def-Builder (Wunsch des Nutzers wie bei C4l). Abnahme: Fotoserie
      jeder Figur im Gehen (Render-Target in der Scratchpad-Kopie), Phasen nebeneinander und
      als Bewegung geprüft, dazu eine Pixelprüfung, dass Fußlinie und Zuschnitt über alle
      Phasen gleich bleiben; zuletzt Sichtprüfung durch den Nutzer *(erledigt 2026-10-09:
      `tools/bilder/gang.py`, acht Phasen je Figur, `c4m_gang.py`; Sichtprüfung durch den
      Nutzer offen, Zeitlupe in `docs/bilder/gang.gif`)*
- [x] **C4n Natürlicher Schwertschlag der Soldaten** (Wunsch des Nutzers, 2026-10-09): die
      Soldaten sollen ihre Schwerter natürlich schwingen, auch hier mit maximalem Aufwand in
      der Animation. Bisher ist die Waffe ins Bild der Miliz und des Spähers gemalt und
      bewegt sich beim Angriff gar nicht; nur das Werkzeug der Dorfbewohner schwingt
      (`SwingAngle`, als gedrehtes Einzelbild). Nötig ist ein eigener Schlagablauf je
      Soldat: Ausholen mit Gewichtsverlagerung, Hieb mit Hüft- und Schulterdrehung, Treffer
      im Takt von `BuildingCombat.RELOAD_SECONDS` (alle 2 s), Zurücknehmen in die Grundstellung;
      der Späher schlägt vom Pferd aus, das dabei ruhig steht. Mehrere Phasenbilder statt
      eines gedrehten Schwerts, Waffe fest in der Hand, Treffpunkt an der Gebäudekante.
      Erzeugen mit dem High-Def-Builder wie bei C4m, Abnahme ebenso: Fotoserie jeder Phase
      und als Bewegung, Treffer im Bild genau beim Abzug der Stärke, Sichtprüfung durch den
      Nutzer *(erledigt 2026-10-09 mit `tools/bilder/schlag.py`: Miliz-Hieb und Speerstoß des
      Spähers, `c4n_schlag.py`; offen: der Bogenschütze spannt beim Angriff noch nicht den Bogen;
      Sichtprüfung durch den Nutzer, Zeitlupe in `docs/bilder/schlag.gif`)*

### C5 · Bauen [X] — erledigt

- [x] Bauzeiten je Gebäude — Richtwerte aus AoE II, die Spezifikation nennt keine: Haus
      25 s, Mühle und Lager 35 s, Farm 15 s, Kaserne 50 s, Stadtzentrum 150 s *(Agent C5a)*
- [x] Bauplatz-Logik: Kollision, freie Fläche — `TileMap.CanPlaceBuilding`, dazu Nebel und
      Einheiten; seit C5b für jedes Gebäude des Baumenüs *(Agent C2c, C5b)*
- [x] Mehrere Arbeiter mit abnehmendem Ertrag — Formel aus AoE II: n Arbeiter bauen
      (n + 2) / 3-mal so schnell wie einer, vier also doppelt so schnell *(Agent C5a)*
- [x] Baumenü: H Haus, M Mühle, F Holzfällerlager, B Bergbaulager; ein Linksklick mit
      Dorfbewohnern auf eine eigene Baustelle schickt sie zum Mitbauen *(Agent C5b)*
- [x] Lager als Abgabestellen, sobald sie fertig sind; ihre Erbauer sammeln danach gleich
      die passende Ressource in der Nähe *(Agent C5b)*
- [x] Eigene Grafik für Baustelle (mit Fortschrittsbalken), Mühle, Holzfällerlager und
      Bergbaulager *(Agent C5c)*
- [x] Liegengebliebene Baustellen: wer ein Gebäude fertig hat und nichts zu sammeln
  bekommt, baut an der nächsten unfertigen eigenen Baustelle im Umkreis von 8 Kacheln
  weiter *(Agent C5d)*
- [ ] Offen: Ein beladener Dorfbewohner, den man bauen schickt, verliert seine Traglast —
  AoE behält sie
- [x] **C5f Farm** — Dorfbewohner wählen, Taste G (Getreide) drücken, einen freien
  3×3-Platz anklicken: 60 Holz, 9 Kacheln je 175 Nahrung, alle begehbar (wie ein
  Beerenbusch). Die Ernte geht an Mühle oder Stadtzentrum, nach der Ernte wächst jede
  Kachel in ~100 s wieder nach. Logik in `TileMap.PlantCrop` und `TileMap.RegrowCrop`;
  Abnahme als `farm`-Gruppe in `tools/spielablauf` *(2026-10-03)*

### C6 · HUD dreiteilig [R]

- [ ] Ressourcenleiste oben: `[Holz] [Nahrung] [Gold] [Stein] [Bev. 37/45] « Feudalzeit »`
- [x] Bevölkerung rot bei erreichtem Limit *(Agent C2b)*
- [ ] Kommandoleiste unten: Aktionssymbole – Einheiteninfo – Minimap
- [x] Befehlstasten mit Symbolen (Qwen-Image, `tools/bilder`); Name, Kürzel und Kosten
      nennt die Leiste, solange die Maus auf einer Taste steht *(Agent C6i)*
- [x] Leisten im Stoff des Zeitalters: rohe Holzbohlen, Eichenbohlen, Quadermauer, grüner
      Marmor mit Goldkante (`PanelAssets`, `DrawPanel`); dunkles Feld hinter dem Hilfetext
      *(Agent H1)*
- [x] Rohstoffsymbole oben als Bilder, doppelt so groß (`ResourceIcons`) *(Agent R1)*
- [ ] Minimap: kleine Karte rechts unten in der Kommandoleiste, in derselben Ansicht wie
      die Spielkarte (zurzeit Draufsicht, Norden oben; Entscheidung 2026-10-03, statt
      der Raute aus dem AoE-II-HUD) — Farben nach dem Referenzbild `docs/overview.jpg`:
      Grün Land, Braun Wald, Blau Wasser, weiße Punkte eigene Einheiten, dunkler Rahmen.
      Feinde rot, Kameraausschnitt als Rechteck *(Agent C6m, C6d)*

### C7 · Terrain-Rendering vervollständigen [R]

- [x] Nebel des Krieges: unerforscht schwarz, erforscht abgedunkelt, fremde Einheiten
      nur in Sicht; ein Klick in den Nebel ist ein Laufbefehl *(Agent C7n)*
- [x] Baustellen decken keinen Nebel auf, Sicht spendet erst das fertige Gebäude
      (`BuildingEntity.IsUnderConstruction`) *(Agent C7b)*
- [x] Bäume als prozedurale Pixel-Art — bestand schon (`BuildForestTexture`, `DrawTree`)
- [x] Beeren, Stein, Goldminen als Textur statt Farbfläche — bestand schon
      (`BuildBerryTexture`, `BuildMountainTexture`, `BuildGoldTexture`)
- [ ] Schatten und Beleuchtung
- [x] Stadtzentrum skaliert im Ganzen mit dem Zoom *(Agent E9)*
- [x] Gebäude als Sprites aus Qwen-Image (`tools/bilder`, freigestellt mit BiRefNet), Fahnen
      und Banner in Spielerfarbe; Baustellen und die Farm zeichnet weiter der Code *(Agent C6g)*
- [x] Gebäude bewegen sich: das Windrad der Mühle dreht sich (Qwen-Image hat die gemalten
      Flügel aus dem Mühlenbild entfernt, Gebaeude/muehle_ohne, das Flügelkreuz
      Gebaeude/muehle_fluegel dreht der Code um die Nabe), und die Fahnen auf Haus, Mühle,
      Wachturm und den Lagern wehen - das Tuch in Streifen, die eine Welle hebt und
      senkt (DrawWavingFlag, FlagWave) *(Agent C6a)*
- [ ] **C6f Feuer in der Schmiede** (Hinweis des Nutzers, 2026-10-07): das Feuer der
      Schmiede ist nur ins Bild gemalt und steht still. Es soll flackern und glühen, wie die
      Mühle sich dreht und die Fahnen wehen. Die Feuerstelle je Bild messen (wie
      `DoorCenters`, `MillHubs`), für die Schmiede in Feudal-, Ritter- und Imperialzeit
      (`Content/Gebaeude/<zeitalter>/schmiede_*`). Darüber Flammen, Glut und Funken, entweder
      aus dem Code oder als Shader wie `Weizen.fx`, eventuell dazu etwas Rauch aus der Esse.
      Baustellen brennen nicht
- [x] Dorfbewohner als Sprite (Qwen-Image) mit Bewegung aus dem Code: wippt beim Gehen, holt
      beim Sammeln und Bauen aus, atmet im Stehen, blickt in Laufrichtung, Schatten und
      Traglast-Bündel *(Agent C6v)*
- [x] Dorfbewohner gehen: zwei Laufbilder je Spielerfarbe (Einheiten/dorfbewohner_lauf1,
      _lauf2), für die Qwen-Image nur die Beine neu gemalt hat - Rumpf und Faust bleiben, das
      Werkzeug sitzt weiter richtig; im Takt des Wippens Schritt, Stand, Gegenschritt,
      Stand (VillagerWalkPhase) *(Agent C7v)*
- [x] Werkzeuge als eigene Sprites in der Faust: Axt, Spitzhacke, Hammer, Sichel, Hacke und
      Angel je nach Arbeit; beim Arbeiten holt das Werkzeug aus und schlägt zu, die Figur
      steht still. Die Hacke hat Qwen-Image per Inpainting aus dem Dorfbewohner-Bild
      entfernt *(Agent C6t)*
- [x] Gras aus einem großen, kachelbar gemachten Grasbild (Qwen-Image), vier Bildpixel je
      Welteinheit, wiederholt alle acht Kacheln; Schaf und Beerenbusch auf durchsichtigem
      Grund darüber; Wald mit fünf Baumarten als Sprites mit Stamm statt runder
      Kronen *(Agent C7g)*
- [x] Sand und Wasser aus kachelbaren Bodenbildern (Qwen-Image), das Wasser in zwei
      gegeneinander treibenden Lagen; Stein und Gold als freigestellte Haufen auf Gras,
      zwei Varianten je Rohstoff; Gras statt dunkler Kacheln unter den Bäumen; der Boden
      in einem eigenen Durchgang mit wiederholender Abtastung (`DrawGround`) *(Agent C7r)*
- [x] Felder aus Acker und Weizen statt der gezeichneten Farmtextur: beide kachelbar
      (Boden/acker, Boden/weizen), die Kante des Felds als dunkler Erdstreifen, und der
      Weizen wächst sichtbar beim Nachwachsen (WheatLook aus Vorrat und FarmRegrow);
      FARM_FOOD und FARM_REGROW_SECONDS als Konstanten in TileMap *(C7f)*
- [x] Farm als ein Bild über das ganze Feld: Weizenreihen mit Holzzaun (Felder/weizen) und
      dasselbe Feld abgeerntet (Felder/acker, per Rechteckmaske aus dem Weizenbild
      ausgebessert, damit Reihen und Zaun deckungsgleich bleiben); jede Kachel zeigt ihren
      Teil (FarmCol, FarmRow, FieldPart), der Zaun ersetzt die gezeichnete Feldkante *(Agent C7h)*
- [x] Schafe und Rehe als freigestellte Sprites (Tiere/, je zwei Varianten) in der
      Zeilenschicht; jedes Tier mit eigenem Wandertakt (WildAnimal), einem sichtbaren
      Schritt von Kachel zu Kachel, Blickrichtung und Schatten *(Agent C7t)*
- [x] Tiere gehen: je Tier zwei Laufbilder (Tiere/<name>_lauf1, _lauf2), für die Qwen-Image
      nur die Beine neu gemalt hat; uebernehmen.ps1 schneidet alle Bilder einer Figur auf
      dasselbe Rechteck (Feld "figur"), und solange ein Schritt läuft, wechseln Schritt,
      Stand, Gegenschritt, Stand (WalkPhase) *(Agent C7u, C7w)*
- [x] Geschlachtete Tiere: sobald ein Dorfbewohner an einem Schaf oder Reh sammelt, ist es
      geschlachtet (WildAnimal.Slaughtered) - es zeigt nur noch sein Fleisch
      (Tiere/fleisch_schaf, fleisch_reh) und bleibt liegen, auch wenn der Dorfbewohner
      abliefert oder abgezogen wird *(Agent C7s)*
- [x] Wald als Blätterdach: dunkler Boden als Kachel, Baumkronen als eigene Figuren
      über Kachelgrenzen hinweg *(Agent G1, G3)*
- [x] Wasser: zwei überlagerte Wellen in drei Varianten, festes Rauschen ohne Flimmern,
      Uferlinie mit Schaumstrich *(Agent G2, G4)*
- [x] Boden aus einem Shader: weiche Ufer und Strände, Wassertiefe, Wellen, Schaum und
      Fische unter der Oberfläche, Gras mit Farbschwankungen, Trampelpfade *(Agent G5)*
- [x] Fische, die man als Fische erkennt: Körper, gegabelte Schwanzflosse, Brustflossen
      und Schwanzschlag, jeder auf seiner eigenen Bahn, mit Schatten auf dem Grund und
      Ringen an der Oberfläche (`FishPath`, `FishMask`, `FishRing` in Boden.fx) *(Agent G6)*
- [x] Weizen im Wind: die Ähren neigen sich, Böen laufen als helle Bänder über die Felder,
      der Zaun steht still (`Content/Effects/Weizen.fx`, `WheatWind`, `DrawWheat`)
      *(Agent G7, G7f)*; die Felder zeigen ihr Bild ohne den hellen Grasrand außerhalb
      des Zauns (`FieldPart`) *(Agent G8)*
- [x] Bäume im Wind: jeder Baum wiegt sich, der Stammfuß steht, die Krone schwingt aus -
      in denselben Böen wie der Weizen (`Wind.Gust`, `Wind.TreeSway`, `Wind.SwayStrips`;
      Gruppe `wind` in `tools/spielablauf`) *(Agent G9)*
- [x] Natürlicheres Gehen: Laufbilder und Wippen nach der gelaufenen Strecke statt nach der
      Uhr (keine rutschenden Füße), leichte Vorlage statt Kippeln von Seite zu Seite,
      Anfahren und Abbremsen, gerade Wege über freies Land statt Kachel-Zickzack
      (`Gait`, `TileMap.IsSegmentWalkable`, `Unit.Pace`); Schafe und Wild gleiten weich von
      Kachel zu Kachel und wippen nur, solange sie Fahrt haben. Gruppen `gang` und `gehen`
      in `tools/spielablauf` *(Agent H1, H2, H3)*
- [x] Blickrichtung in Seitenansicht: Dorfbewohner blicken zur Seite, in die sie gehen - fast
      senkrecht zur Seite ihres Ziels -, bei der Arbeit zu Baum, Stein, Gold oder Baustelle,
      und stehen dafür bevorzugt seitlich daneben statt darüber, darunter oder mitten im
      Haufen (`Gait.FacingLeft`, `StandCell`, `SiteStandCell`); Schafe und Wild machen kaum
      rein senkrechte Schritte (`WanderStep`). Gruppe `blick` in `tools/spielablauf` *(Agent J)*
- [x] Ein Klick ins Schwarze wirkt immer: Laufbefehle planen nur mit dem, was der Spieler weiß
      (`TileMap.FindPathKnown`, nie gesehene Kacheln gelten als begehbar) - vorher blieb die
      Einheit stehen, wenn darunter Wasser oder ein fremdes Gebäude lag, und verriet es so.
      Stößt sie unterwegs auf ein Hindernis, plant sie neu, statt es zu betreten; ist das Ziel
      gesperrt, hält sie so nah wie möglich. Gruppe `dunkel` in `tools/spielablauf` *(Agent K)*
- [x] Bäume mit offener Krone in acht Arten, jeder Baum einzeln nach seinem Stammfuß in der
      Tiefenschicht, zwei je Waldkachel, Höhe je Art, Laub- und Nadelbäume in Hainen
      (`TreeAssets`, `TreeSpot`, `DrawTree`) *(Agent T1a, T1b)*
- [x] Gras feiner: vier neue Sorten mit feinen Halmen, im Shader mit eigenem Maßstab
      (`GRASS_TEXELS`, `GrassScale`) *(Agent G11)*
- [x] Wiese in drei Höhen: kurzes Kleegras, ungemähte Wiese, hohes und hohes trockenes Gras,
      Blumen und ausgedörrte Erde (`Meadow`, `TallGrassAt`); im hohen Gras stehende Büschel, in
      denen Figuren bis zur Hüfte stecken (`DrawGrassTuft`, `DrawGrassAroundFeet`) *(Agent G12, G13)*
- [x] Beerenbüsche als Sprites in drei Arten statt der gezeichneten Kachel (`DrawBerryBush`)
      *(Agent G13)*

### C10 · Kamera [R]

- [x] Zoomen mit dem Mausrad, **auf den Cursor zu** statt auf die Bildmitte
      (RTSGameplayScreen, multiplikativer Schritt 1,12; Grenzen MIN_ZOOM 0,5 / MAX_ZOOM 2,0
      bis 1080 px Fensterhöhe, darüber wachsen sie und der Startzoom mit *(Agent C10z)*)
- [x] Der Zoom folgt der Fensterhöhe, der Ausschnitt bleibt gleich groß — vorher begann das
      Spiel mitunter mit Zoom 1 statt 1,25, wenn das Fenster seine Größe erst nach dem
      Laden bekam *(Agent C10s)*
- [x] Falle umgangen: ``ScrollWheelValue`` zählt seit Programmstart kumuliert —
      ohne Startwert springt der erste Frame auf Maximalzoom
- [x] Kamera an den Kartenrand geklemmt (`ClampCamera()`) — vorher zeigte der
      Startausschnitt überwiegend Fläche außerhalb der Karte, siehe Screenshot
      vom 2026-09-23 in `docs/`
- [ ] **Offene Entwurfsfrage:** die Karte ist 64×64 Kacheln à 32 px = 2048 px,
      ein typisches Fenster ist breiter. Bei Zoom 1 passt die ganze Karte ins
      Bild und wird jetzt zentriert — links und rechts bleibt Rand. Entweder
      größere Karte, oder MIN_ZOOM dynamisch so setzen, dass die Karte das
      Fenster immer füllt (schließt dann aber die Gesamtübersicht aus)
- [ ] **C11r Rechteckige Karte** (Hinweis des Nutzers, 2026-10-07): die Karte soll
      rechteckig sein, ungefähr im Seitenverhältnis des Bildschirms, statt quadratisch.
      Heute baut `RTSGameplayScreen.LoadContent` sie mit `new TileMap(side, side, …)` aus
      `MapSizes.Side`. Künftig Breite und Höhe je Kartengröße (etwa 16:9 bzw. 16:10 bei
      gleicher Fläche wie bisher, sodass `MapSettings.ForSize` die Ausstattung weiter nach
      der Fläche richtet). Prüfen, was quadratisch denkt: Startplätze, Kartengenerator,
      Minimap (rechteckig statt quadratisch), `ClampCamera`, Nebel. Löst wohl auch die
      Entwurfsfrage oben. Abnahme: `tools/kartenpruefung -- groessen`, `tools/spielablauf --
      karten`
- [x] Kantenscrollen mit der Maus, 8 Pixel Randstreifen *(Agent C10)*
- [x] Karte mit gedrückter linker Maustaste ziehen, ab 6 Pixeln Weg *(Agent C8a)*

### C8 · Auswahl & Steuerung [R]

- [x] Selektion zeigt HP/Status — für eine einzeln gewählte Einheit: Name, LP und
      bei Dorfbewohnern die Traglast *(Agent C1c)*
- [x] Befehl → Bewegung oder Sammeln — seit C8a per kurzem Linksklick, einmal pro Klick
- [x] Lasso-Auswahl per Mauszug — seit C8a mit der rechten Taste (bestand schon als Rechteck)
- [x] Maussteuerung auf Wunsch des Nutzers umgestellt: rechts markieren, links ziehen
      verschiebt die Karte, kurzer Linksklick befiehlt *(Agent C8a)*
- [x] Ein Linksklick auf eine eigene Einheit wählt sie aus, statt sie wegzuschicken —
      vorher zog ein Klick zum Auswählen einen Bauarbeiter vom Bau ab *(Agent C8b)*
- [x] Befehlstasten und Minimap reagieren auf Klicks — vorher kam kein Klick in die
      untere Leiste an. Bautasten nur, solange ein Dorfbewohner ausgewählt ist; Q und
      „." immer *(Agent C8c)*
- [x] Befehlstasten so breit wie ihr Name, der Name sitzt in der Taste — „Bergbaulager" und
      „Untätig" liefen in die Nachbartaste, alle Namen unten aus der Taste *(Agent C8d)*

### C9 · Basis-KI [X]

- [ ] Einheit sucht nächste Ressourcenquelle und sammelt

### C12 · Straßen ab Ritterzeit [R]

- [ ] Baustufe „Straße“ (Taste frei zu vergeben) ab der **Ritterzeit** freigeschaltet —
  `AgeRules.RequiredAgeOf`, Baumenü und Befehlstasten wie beim Wachturm
- [ ] Dorfbewohner bauen Straßen kachelweise (Bauzeit je Kachel, nicht je Gebäude) —
  Platz ist jede bebaubare Kachel, auch neben anderen Straßen
- [ ] Wegsuche bevorzugt vorhandene Straßen: A* bekommt einen Bonus für Straßenkacheln,
  sodass Dorfbewohner auf ihrem Weg immer eine Straße nehmen, wenn eine dazwischenliegt;
  ohne Straße bleibt der bisherige Lauf
- [ ] Straße sichtbar zeichnen (kachelbares Bild, wie Gras/Weizen), auch unter Nebel
- [ ] Abnahme: neue Gruppe `strassen` in `tools/spielablauf` — Dorf auf Ritterzeit,
  Straße bauen, beobachten dass sich ein laufender Dorfbewohner auf der Route die Straße
  nimmt und nicht mehr im Gras

### C13 · Dorfkern, Kaserne, Mauern und Krieger (Feudalzeit) [X]

- [ ] **Dorfkern** ab der Feudalzeit neu baubar: ein zweiter (weiteres) Dorfzentrum,
  in dem Dorfbewohner — also auch Krieger — ausgebildet werden; **teuer** wie im AoE II:
  400 Holz, Bauzeit 150 s (Richtwerte sind bereits in AoE-II-Tabelle eingeplant)
  *(Agent C5a)*. `AgeRules.RequiredAgeOf`, Baumenü, Taste, eigene Grafik je Zeitalter
  wie Wachturm. Platz wie jedes andere Gebäude (`TileMap.CanPlaceBuilding`).
  Stand L2: ein weiteres Stadtzentrum ist wie in AoE II ab der Ritterzeit baubar (Taste Z,
  275 Holz und 100 Stein); früher und teurer wie oben beschrieben ist noch offen
- [x] **Kaserne** baubar - wie in AoE II und `AgeRules` schon ab der Dunklen Zeit, nicht
  erst ab der Feudalzeit: Taste K in der zweiten Tastenreihe, eigene Grafik je Zeitalter unter
  `Content/Gebaeude/<zeitalter>/`, Bauzeit 50 s *(Agent C5a, L1, L2)*
- [ ] Dorfkerne und Kasernen sind wie das Start-Stadtzentrum auswählbar (Klick) und
  bilden Dorfbewohner aus. Stand B1/P1: jedes eigene Stadtzentrum ist auswählbar und bildet
  Dorfbewohner aus, die Kaserne die Miliz
- [ ] **Mauern** ab der Feudalzeit baubar: `StoneWall` (Bau `PalisadeWall` ist im
  `BuildingType`-Enum schon da, aber nicht verdrahtet), mit **Stein** kachelweise bauen
  wie Straßen (C12: `AgeRules.RequiredAgeOf`, Baumenü, Taste). Mauern sind für
  **jeden** unpassierbar — Dorf- wie Feindbewohner laufen nur drum herum.
  Stand L2: Palisadenmauer (Taste P, ab der Dunklen Zeit, 2 Holz) und Steinmauer (W, ab der
  Feudalzeit, 5 Stein) stehen im Baumenü, je Kachel ein Stück; nach einem Stück bleibt der
  Setzmodus an. Offen ist die Abnahme `mauern` (Umlaufen, Abreißen)
- [ ] Mauer ist zerstörbare Struktur (wie Gebäude HP aus dem `BuildingType`-Werten):
  nur ein angreifender Feind, der ein Stück Mauer **zerstört**, kommt durch; nach
  Zerstörung ist die Kachel wieder begehbar
- [ ] Abbauen eigener Mauern: ein eigener Dorfbewohner kann (wie bei Gebäuden)
  ein eigenes Mauerstück **zerstören** — Taste/Befehl auf das Mauerstück, dann
  wird es Stück für Stück abgerissen und die Kachel ist wieder frei; die eigene
  Mauer ist danach für niemanden mehr eine Barriere. Abnahme in `tools/spielablauf`,
  Gruppe `mauern` (bauen, Feind läuft an → muss umlaufen; eigener Dorfbewohner
  reißt ein Stück ab → die Lücke ist für beide Seiten frei durchgängig)
- [ ] Ausgebildete Dorfbewohner stehen **neben dem ausgebildeten Gebäude**, nicht auf
  ihm
- [ ] **Krieger** als neue Einheit, ab dem **Feudalzeitalter** in Dorfzentrums/Kern
  auszubildbar (Kaserne optional):
  - können nichts abbauen/sammeln (reine Kämpfer)
  - kämpfen besser als Dorfbewohner (`DamageCalculator`/Stats in AoE.Core),
    Ausbildung teurer als ein Dorfbewohner
  - eigenes Sprite (Stand + Lauf), Färbung wie Dorfbewohner
  - zählt gegen das Bevölkerungslimit (C2)
  - Abnahme: `krieger`-Gruppe in `tools/spielablauf` — zweiter Dorfkern bauen,
    Krieger bilden, stehen neben dem Dorfzentrum, kämpfen stärker als ein Dorfbewohner

### C14 · Gebäude auswählen, angreifen, ausbilden und forschen [X]

Wunsch des Nutzers (2026-10-06): ein Gebäude auswählen, dort Dinge herstellen, seinen Status
sehen; unter Angriff sinkt seine Stärke. Gewählter Umfang: die bestehende Ausbildung ans
Gebäude, erste Soldaten, Forschungen; angreifen nur eigene Einheiten per Befehl.

- [x] Gebäude auswählen: Rahmen, Lebensbalken, Status in der Leiste; Q und A am ausgewählten
      Stadtzentrum *(Agent B1)*
- [x] Gebäude angreifen: eigene Einheiten auf Befehl, Schaden je Schlag nach `BuildingCombat`
      in AoE.Core, Zerstörung gibt die Kacheln frei *(Agent K1, K2)*
- [x] Soldaten: Kaserne Miliz, Schießstand Bogenschütze, Stall Späher, je Taste Q, Kosten und
      Zeiten nach AoE II (`Products`) *(Agent P1)*
- [x] **P2 Forschungen** — Webstuhl (Stadtzentrum, W), Pferdekummet (Mühle), Doppelaxt
      (Holzfällerlager), Goldbergbau (Bergbaulager), Schmiedekunst, Befiederte Pfeile und
      Schuppenpanzer (Schmiede, Q W E), Maurerkunst (Universität); Regeln und Wirkung in
      AoE.Core (`Economy/Research.cs`), Tasten nach `Researches` *(Agent P2a, P2b)*
- [ ] Die KI greift noch keine Gebäude an

---

# Block D — Doku

### D1 · `docs/PROJEKT_STRUKTUR.md` reparieren [R]

Die Datei ist **ab „Wichtige Dateien" textuell zerschossen** — sieht nach einem
fehlgeschlagenen Patch aus:

- Pfade, die nicht existieren: `src/AfempireCore/core.Pathfinding.cs`,
  `AgeofempiresClone.Core.Screens/TSGameplaScreen.ccsproj`, `src/AoE.Map/VisibilitySystem.Vsible.cs`
- Mermaid-Diagramm mit kaputter Syntax (`Combat[DamageCalc]<br/>Schaden:::src`)
- Build-Status-Tabelle mit zerbrochener Zeile
- Verweist auf `AoE1-Readme.md` und das `.webp` im Root — beide gelöscht
- `MapGrid.cs` als eigene Datei gelistet — liegt tatsächlich in `VisibilitySystem.cs:241`

- [x] Komplett neu geschrieben *(Agent D1)* — erfundene Pfade raus, Build-Tabelle gegen den
      echten Stand, kein Mermaid mehr, Stand 2026-09-23

### D4 · Zielplattformen in den MD-Dateien festhalten [L]

- [x] `README.md`: Abschnitt „Zielplattformen"; Android/iOS als nicht unterstützt vermerkt *(Agent D4)*
- [x] `AgeOfEvolutions/game_plan.md`: Hinweis auf die Entfernung der Mobile-Projekte *(Agent D4)*
- [x] `docs/PROJEKT_STRUKTUR.md`: im Kopf vermerkt *(Agent D1)*

### D2 · `README.md` im Root [L]

Existiert nicht.

- [x] Kurzbeschreibung, Build-Anleitung, wie man das Spiel startet *(Agent D2)*

### D3 · `.github/copilot-instructions.md` [L]

Enthält reines Azure-Boilerplate ohne jeden Bezug zum Projekt.

- [x] Durch projektbezogene Hinweise ersetzt *(Agent D3)*

---

# Erledigt

- [x] Projektstruktur konsolidiert, MD-Dateien zentral in `docs/`
- [x] .NET-10-Migration: AoE.Core, Tests, Demo, Spiel-Core, DesktopGL, WindowsDX
- [x] Build läuft fehlerfrei durch (AoE.Core + DesktopGL, 0 Warnungen)
- [x] AoE.Core als Projektverweis eingebunden *(seit Block B im Spiel genutzt)*
- [x] 267 Unit-Tests grün
- [x] Kartengenerierung `TileMap.cs`: Seen mit Falloff, Beeren, Schafe, Wildschweine,
      Stein/Gold, PvP-Startpositionen
- [x] Wasser-Animation
- [x] Menü führt zu `RTSGameplayScreen`
- [x] Platformer-Reste des MonoGame-Beispiels entfernt
- [x] Root-Solution `AgeOfEmpire.slnx`, `.gitignore`, `README.md`
- [x] Zielplattformen auf PC + Mac festgelegt, Mobile-Projekte entfernt
- [x] Endliche Ressourcenvorkommen (C3)
- [x] Dorfbewohner-Loop (C1), Kantenscrollen (C10), Startplatz (E5)
- [x] Nebel des Krieges (C7n), Maussteuerung (C8a), Gebäude aus AoE.Core (B6)
- [x] Auswahl an der ganzen Figur (E8), Stadtzentrum skaliert mit dem Zoom (E9)
- [x] Stein und Gold abbaubar (E10), Startrohstoffe (C1r), Fischen (C1f), Wald und Wasser (G1–G4)
- [x] Bevölkerungslimit, Ausbildung und Häuser (C2), Traglast bleibt erhalten (C1t)
- [x] Bauen mit Bauzeit, Bauarbeitern, Lagern und eigener Grafik (C5)
- [x] Weiterbauen an liegengebliebenen Baustellen (C5d), Linksklick-Auswahl (C8b),
  Werkzeug `tools/spielablauf`
- [x] **C5f Farm** — Getreidefeld (Taste G, 3×3, 175 Nahrung/Kachel, Regrow ~100 s),
  neue `farm`-Gruppe in `tools/spielablauf` als Abnahme *(2026-10-03)*
- [x] **L1, L2 Die übrigen Gebäude** — Kosten, Bauzeit, Größe und Werte für jeden
  Gebäudetyp (L1), zweite Tastenreihe mit Symbolen und Bildern je Zeitalter, Mauerreihen,
  Gruppe `neubauten` in `tools/spielablauf` (L2) *(2026-10-06)*
- [x] **B1, K1, K2, P1, P2** — Gebäude auswählen und angreifen *(2026-10-06)*, Soldaten aus
  Kaserne, Schießstand und Stall, acht Forschungen in sechs Gebäudearten *(2026-10-09)*

---

## Notizen

- **Lokale Agents:** `tools/ollama-agent/` verteilt Punkte von hier an ein Ollama-Modell
  (`qwen3.8:27b`, 256K Kontext) und nimmt sie per Build, Test und Prüfskript ab
  (`py run_tasks.py --list`). Die IDs sind dieselben. Sinnvoll für `[L]` und mit
  Sichtung für `[R]`; `[X]` gehört nicht dorthin.
- Priorisierung folgt `docs/AgeOfEmpires.md`, Kap. „Relevanz für dieses Projekt" (Z. 672–715)
- **Bekannter Engpass laut Spec:** es existiert *kein einziges AoE-taugliches Grafik-Asset*.
  `RTSGameplayScreen` zeichnet prozedural erzeugte 32×32-Pixelart statt echter Grafiken. Alle Punkte außer C6/C7 und der
  isometrischen Darstellung lassen sich damit trotzdem vollständig umsetzen und testen.
- **Bewusst nicht nötig** fürs Grundgerüst: Zivilisationsboni, Reliquien, Mönche, Handel,
  Seekampf, Formationen, Kampagnen.
