# AoE-Clone — TODO / Meilensteine

**Stand:** 2026-10-23 · verifiziert durch Build + Testlauf, nicht aus Doku übernommen.

## Wiederaufnahme — hier weitermachen

**Bilder der Konter-Einheiten: Speerkämpfer, Plänkler, Ritter (F2)** (2026-10-23) — Wunsch: „kannst du F2 · Konter-Einheiten (Speerkämpfer, Plänkler, Ritter) die Bilder dazu generieren“. 86 Bilder in `Content/Einheiten` samt mgcb-Einträgen: je Spielerfarbe Stand und 8 Gehphasen (`gang.py`), Speerkämpfer und Ritter dazu 8 Schlagphasen (`schlag.py`: Speerstoß wie beim Späher, Ritter haut aus dem Ellbogen); der Plänkler bekommt wie der Bogenschütze keine Schlagbilder. Qwen-Einträge in `bilder.json` (Gruppe `soldaten`: `speer*`, `plaenkler*`, `ritter*`), Gelenke in `gang.json`/`schlag.json`. Zielbreiten so, dass die Figuren so viele Pixel hoch sind wie Miliz (216) und Bogenschütze (217): Speerkämpfer 216 breit (der Speer), Plänkler 141; Ritter 149 × 175 für Maßstab 1,4 (Späher 1,35 bei 169). Im Spiel noch nicht eingebunden - `SoldierSprites` braucht `(UnitType.SpearMan, "Einheiten/speerkaempfer", 1f)`, `(UnitType.Skirmisher, "Einheiten/plaenkler", 1f)`, `(UnitType.Cavalry, "Einheiten/ritter", 1.4f)`. Werkzeuge erweitert: `gang.py` `"decke"` (bunte Schabracke über den Pferdebeinen bleibt Rumpf, der Saum darf unter dem Stoff liegen), `schlag.py` `"schaft_fuellen"` (Schaft unter der Faust, sonst wandert beim Gleiten eine Lücke mit) und `"faust_aus_standbild"`. Lehren: Ritterpferd mit langer Schabracke, unter der alle vier Beine hervorkommen - bei halber Decke rissen Schwung und Bauch Löcher; Qwen malt unter einer Schulterplatte immer wieder einen Oberarm (7 Versuche), daher der Hieb aus dem Ellbogen; Rot erst ab Sättigung 0,75 (`blau_saettigung`), sonst wird die bläuliche Rüstung rosa; zwei `qwen_image.py` nie gleichzeitig (`/free` und BiRefNet zwischen fremden Qwen-Läufen).

**Gebäude schießen: Wachturm, Stadtzentrum und Burg beschießen sichtbare Feinde (F1)** (2026-10-23) — Block F abgearbeitet, zuerst die größte Lücke: „Kein Gebäude schießt“. Werte stehen in AoE.Core: Wachturm 5 Angriff / 8 Reichweite (war schon da, tat nichts), **neu Stadtzentrum 5/6** (`CreateTownCenter`), Burg 11/8 (stand schon in der Tabelle); Schusstakt `BuildingCombat.BOMBARD_SECONDS` 2 s wie bei den Einheiten. Der Schaden ist der **allgemeine `DamageCalculator`** (neu `BuildingCombat.DamageToUnit`): Angreifer hat Range > 0, also zählt Fernkampf gegen die Fernkampf-Rüstung des Ziels, mindestens 1 — kein neues Regelwerk. Neue Teildatei `Screens/RTSGameplayScreen.Beschuss.cs`: `UpdateBombard(dt)` hängt in `UpdateUnits` nach `UpdateFallen`; je fertigem Gebäude mit eigenem Angriff (`BuildingCombat.Shoots` = Angriff > 0 und Range > 0 — trifft genau Turm, Zentrum, Burg) das **nächste sichtbare Feindziel** in Reichweite (euklidisch, halbe Kachel Großzügigkeit, Zielkachel muss für den Besitzer `IsTileVisible` sein — der Nebel schützt), Takt auf dem neuen transienten `Building.BombardTimer` (nicht im Spielstand; geladen beginnt er bei 0). Der Pfeil startet wie bei einer Figur eine Flugzeit vor dem Schlag und landet genau beim Treffer (`_arrows`, A3-Bahn, Klang „bogen“ an der Gebäudemitte, `RegisterDamageTaken`); Tote räumt der Aufräumer am Ende von `UpdateUnits`. **Forschungen:** Maurerkunst wirkt auf Gebäude wie zuvor (LP +10 %, Rüstung +1 — betraf Türme schon); **Befiederte Pfeile heben jetzt auch die schießenden Gebäude** (Angriff +1, Reichweite +1 über `TechEffects.Apply` auf `BuildingEntity { Stats.Range: > 0 }` — wie in AoE II die Bogentechnologien die Türme mitnehmen); die Taste zeigt weiter den alten Kurztext (Prüfung fixiert die Zeichenkette — Nebenbefund). **KI:** `EnemyInfo.IsArmed` (neu, Brücke setzt es für fertige schießende Gebäude); die Angriffswelle **meidet bewehrte Gebäude**, solange ein anderes sichtbar ist, und zieht sonst auf das nächste bewehrte („gezielt angreifen oder meiden“). **Prüfwelt-Haken:** `RTSGameplayScreen.TestBombard` (wie `TileMap.TestNoFog`) — `tools/spielablauf` baut jede Welt ohne Beschuss, weil viele Szenarien neben dem Start-Stadtzentrum stehen (ohne den Schalter liefen pfeile/zielwechsel/autoantwort auf 3 Karten rot: das Zentrum erschoss ihre Testopfer); die neue Gruppe `beschuss` schaltet ihn für sich an. Umgesetzt von Hand (Regel: nie auf qwen3.8:27b wechseln, alles selbst — auch die Prüfung auf Auslagerung hat der Nutzer am selben Tag bekräftigt). Abnahme: Builds DesktopGL + WindowsDX 0 Fehler · `dotnet test` **485/485** (+21: `GebaeudeSchussTests` 19 — Werte, Schuss-Regel je Typ, Fernkampf gegen Fernkampf-Rüstung, mindestens 1, Fletching an Turm/Haus/Bogenschütze, Fletching+Maurerkunst kombiniert, Takt 2 s; +2 KI — Welle meidet das bewehrte Zentrum, greift es aber an, wenn nur bewehrte sichtbar sind) · neue Gruppe `tools/spielablauf beschuss` (auf 3 Karten: erster Treffer genau nach einem Takt nicht früher, Miliz in 4 Kacheln in ≤23 s tot und aus der Liste geräumt, Bogenschütze in 11 Kacheln / Miliz am Haus / Miliz an der Turm-Baustelle unverletzt, Stadtzentrum trifft selbst in 3,5 s) · Mutantenlauf 4 von 4 gefunden (Takt +4 s, Stadtzentrum-Angriff 0, `IsComplete`-Wache aus, KI-IsArmed ignoriert) · breite Regression `linksklick auswahl ziele soldaten angriff verteidigung kampfbild raubtiere wild schafe tuer strassen neubauten bauen weiterbauen bauphasen pfeile farm beweg zielwechsel schatten animation autoantwort turm zeitalter speichern beschuss` auf 3 Karten grün · neue Strukturprüfung `checks/f1_beschuss.py` grün und **gegen den Altstand rot** (11 Punkte, Git-Worktree auf 95cf78e) · `u_vertrag.py` auf der neuen Teildatei · `tools/ai-pruefung` alle Prüfungen ok (beide KI-Instanzen, Wirkung weiter beobachtet) · Startprobe GL + DX. **Offen:** Sichtprüfung durch den Nutzer im laufenden Spiel (feindliche Einheiten am eigenen Stadtzentrum: Pfeile von den Zinnen, Klang, Todeszeitpunkt); feindliche Türme stehen der Ernte näher als vor F1 — die KI weicht ihnen aus, der Mensch muss es selbst.

**Abgleich mit `docs/AgeOfEmpires.md` → neuer Block F** (2026-10-23) — Wunsch: „Kannst du AgeOfEmpires.md durchgehen und prüfen was fehlt. Dann die TODO liste auffühlen“. Jedes Kapitel gegen den Code geprüft; 19 Lücken als F1–F19 nach Priorität, dazu die bewussten Abweichungen (Draufsicht statt isometrisch, Linksklick = Befehl). Größte Lücken: kein Gebäude schießt (F1), nur vier ausbildbare Einheiten ohne Konter (F2), Markt, Kloster, Burg, Werkstatt und Wunder ohne Funktion (F8, F9, F15–F17), fremde Gebäude im Nebel werden live statt im letzten bekannten Zustand gezeichnet (F7). Dabei vier alte offene Haken nach Prüfung im Code als erledigt bzw. gegenstandslos markiert (C9, C10, C13 Ausbildungsplatz, C14 KI greift Gebäude an) und die C4-Notiz auf den Stand gebracht.

**Menühintergrund: Dorfplatz auch hinter „Einstellungen“ und „Spiel laden“** (2026-10-23) — Wunsch: „Der Hintergrund in Einstellungen und Spiel laden muss noch Thematisch angepasst werden“. Hinter beiden Untermenüs lag noch `BackgroundScreen` mit `Layer0_2.png` (blaues Gebirge aus der Platformer-Vorlage, ins 800×480-Raster gestreckt, schwarze Ränder); nur das Hauptmenü verdeckte es mit seinem eigenen Bild. Jetzt zeichnet `BackgroundScreen` das Menübild (`Backgrounds/menu`, Dorfplatz) fensterfüllend in Fensterpixeln mit der Abdunklung 0,3, Pergament als Ersatz, falls das Bild fehlt; `MainMenuScreen` zeichnet kein eigenes Bild mehr, und statt der Schwarzblende über den ganzen Schirm blendet nur seine Tafel (`TransitionAlpha`) — sonst blitzte beim Wechsel Schwarz zwischen zwei gleichen Bildern auf. Ausblenden und Rückkehr des Hauptmenüs dauern wie bei den Untermenüs 0,25 s (der erste Start blendet weiter 1,5 s). Die `Layer*.png` sind damit unbenutzt (noch im mgcb). **Abnahme:** `dotnet build` DesktopGL 0 Fehler · `tools/spielablauf -- menue update` grün · Fotos aus Scratchpad-Kopie (DX, Patch `foto_menue_patch.ps1`): Hauptmenü, Überblendung zu Einstellungen (Dorf bleibt, kein Schwarz), Einstellungen, Spiel laden. **Nebenbefund:** Titel „Settings“, „Display Mode : Windowed“ und „Back“ kommen englisch aus `Resources`.

**Schattenprobleme behoben: Baumschatten-Bänder lagen als Rechtecke am linken Bildrand** (2026-10-23) — `errors/Schattenprobleme.png` und `Schattenprobleme2.png`: türkise, achsgerade Rechtecke, Nutzer: „schein immer auf der Linken Seite zu sein“, „es ändert sich auch“. Ursache in `DrawSwayCastShadow` (seit 4d58f1d): `Draw(info.Tex, quelle, ziel, …)` – MonoGame nimmt bei zwei Rechtecken **erst das Ziel, dann die Quelle**. So landete jedes Band eines Baumschattens bei (0, Py0) in Maskengröße am linken Bildrand, und das Bildschirmrechteck wurde als Maskenausschnitt gelesen (riesig vergrößerte Kronenbögen, Dichte 0,85); die Bäume selbst hatten keinen Schatten an ihrem Platz. Jetzt `Draw(info.Tex, ziel, quelle, …)` in beiden Zweigen, Vertrag korrigiert, `checks/c7_schatten.py` verlangt die Reihenfolge. Diagnose: Spielstand 7 des Nutzers (Kopie) im Scratchpad geladen, Protokoll jedes Schattenzugs + Bild von `_shadowTarget` – kein protokolliertes Ziel lag im Rechteck, Abschalten der Bänder ließ es verschwinden. **Abnahme:** Builds GL + DX 0 Fehler · `checks/c7_schatten.py` grün · `tools/spielablauf -- schatten menue` grün · Fotos Spielstand 7 in GL und DX vorher (Rechtecke links) und nachher (keine; Baumschatten am Stamm nach rechts vorn).

**Bären und Wölfe in Bewegung: je acht Lauf- und acht Angriffsphasen (RT4)** (2026-10-23) — Wünsche (aus der RT-Sitzung, dort am Sitzungslimit abgebrochen): „zudem müssen Bären und Wölfe animiert werden. Vorallem bei bewegung und Angriffen“ und „Beim Wolf und Bären sollten es mehr sein als 8 da Beim Angriff auch mindest 8 zustäzlich generiert werden müssen“; heute „kannst du die Animation der Wölfe und Bären noch fertig machen“. Vorher liefen sie mit zwei Qwen-Laufbildern (Wechsel Lauf 1, Stand, Lauf 2, Stand) und zeigten beim Schlag 0,45 s ein einziges Angriffsbild.

Neues Werkzeug `tools/bilder/tieranim.py` (Skelette und Schlüsselposen in `tieranim.json`): wie `gang.py` ein Bein-Skelett über dem freigestellten Standbild, aber für Fell **weich gebogen** statt an Gelenken geschnitten — jeder Bildpunkt dreht wie eine Gliederkette nacheinander um Hüfte, Knie und Knöchel, jeweils nur so weit, wie er dem Gelenk folgt (an Knie und Knöchel nach dem Winkel um das Gelenk, an der Hüfte über `uebergang` Pixel in den Rumpf); so knicken weder Umriss noch Fell, und die Beine werden am gebeugten Gelenk nicht dünner (lineares Mischen schnürte sie ein, Mischen der zwei stärksten Knochen gab Kanten, eine Schnittlinie am Knöchel zog die Pfoten zu Wirbeln). Abgewandte Beine liegen hinter dem Rumpf, die Knick-Seite der Zwei-Knochen-Rechnung liest das Werkzeug aus dem Bild (vorher klappten die Vorderbeine des Bären schon im Stand um), was unter dem Knöchel liegt gehört ganz zur Pfote, verirrte Einzelpixel fallen weg. **Laufen:** Fußbahnen aus `gang.Bein` (Trab beim Wolf, Viertakt beim Bären), der Rumpf bleibt in Standhöhe und sinkt nur, wenn ein Standbein nicht mehr reicht (`mitte_versatz` schiebt den Stand der fast gestreckten Tierbeine nach hinten), dazu leichtes Wippen und beim Bären Nicken; im Schwung hängt die Pfote am Mittelfuß. **Angriff:** acht Schlüsselposen über Stand- und Angriffsbild — Wolf: ducken, tiefer ducken, Sprung mit gehobenen Vorderläufen und offenem Maul, Biss, nachfassen, zurückziehen, zurück in den Stand; Bär: aufrichten, höher, Pranke oben (die erhobene Pranke des Angriffsbilds bleibt beim Rumpf und schlägt mit seiner Neigung, `"fehlt": true`), Hieb nach unten, durchziehen, zurück. Die Angriffsbilder liegen wie bei `schlag.py` auf einer größeren Leinwand im Maßstab des Standbilds (Pfoten an derselben Stelle, Eckpixel mit Deckkraft 9 legen das Rechteck fest). Neue Gruppe `tieranim` in `bilder.json` (64 Bilder, Zielbreiten 268–322 für die Angriffe); die alten Lauf- und Angriffsbilder in `tiere_lauf` haben kein Ziel mehr und sind nur noch Vorlage; `Tiere/<tier>_angriff.png` aus Content und `.mgcb` entfernt, die Bär/Wolf-Einträge aus `gang.json` ebenso (stehen in `tieranim.json`). Zeitlupe: `docs/bilder/raubtiere.gif`.

Spiel (`RTSGameplayScreen.Raubtiere.cs`, `DrawAnimal`): `LoadPredatorSprites` lädt `_lauf1` bis `_lauf8` nach `_animalWalk` und `_angriff1` bis `_angriff8` nach `_animalAttack` (statt `_animalStrike`). `PredatorWalkFrame` wählt die Laufphase nach der weich gegangenen Strecke (`Gait.WalkFrame` mit `BEAR_STRIDE` 6,85 / `WOLF_STRIDE` 6,0 Welteinheiten aus der Ausgabe von tieranim.py, schräg länger) — die Pfoten rutschen nicht, das Code-Wippen entfällt (steckt im Bild). `PredatorAttackFrame` folgt dem Schlagtakt wie die Soldaten: nach dem Schlag Phasen 3 bis 7 über `PREDATOR_STRIKE_SECONDS`, der Biss (3) genau im Bild des Treffers; steht ein Opfer in Reichweite, holt das Tier in den letzten `PREDATOR_WINDUP_SECONDS` 0,3 s vor dem nächsten Schlag mit 0 bis 2 aus — der erste Schlag gegen jemanden, der eben herankommt, kommt ohne Ausholen. Dafür neu `WildAnimal.Engaged` (nur fürs Bild, nicht im Spielstand), gesetzt in `TileMap.UpdatePredators`. Angriffsbilder werden im Maßstab des Standbilds größer gezeichnet, der Lebensbalken bleibt über dem Standbild.
Umgesetzt von Hand (Regel seit 2026-10-23: nie auf qwen3.8:27b wechseln); Bilder ohne Qwen-Image, nur aus den vorhandenen Stand- und Angriffsbildern gerechnet.
Abnahme: Builds DesktopGL + WindowsDX 0 Fehler · `dotnet test` **464/464** (+1 `MitOpferInReichweiteHoltEsAus`) · neue Gruppe `tools/spielablauf raubtieranim` (Zeitplan der Angriffsphasen in zehn Fällen, Wolf gegen Dorfbewohner: erster Biss sofort ohne Ausholen, jeder Biss in Phase 3 genau im Schlagbild, 3 bis 7 danach und 0, 1, 2 davor der Reihe nach, kein Ausholen ohne Opfer; Laufen von Bär und Wolf gerade und schräg: Phase 0 am Schrittanfang, nur Schritte +1, Zahl der Wechsel passend zur Schrittlänge, im Stand Standbild) auf 3 Karten grün · Mutantenlauf 9 von 9 gefunden (Ausholen ohne Opfer, Biss zu früh, Schrittlänge doppelt, schräg wie gerade, Ausholen rückwärts, Engaged nie/immer gesetzt, Laufbild im Stand, nur vier Laufphasen) · `raubtiere wild schafe hover angriff kampfbild zielwechsel kampfklang speichern pfeile schatten animation` auf 3 Karten grün · Strukturprüfung `checks/rt_raubtiere.py rt3a rt3b rt4` grün, `rt4` gegen den Altstand rot · Startprobe GL + DX · Fotos aus dem Spiel (Prüfbogen aller Lauf- und Angriffsphasen der vier Tiere, Foto-Patch nur in der Scratchpad-Kopie) gesichtet: Pfotenlinie, Maßstab, Lebensbalken. **Offen:** Sichtprüfung durch den Nutzer im laufenden Spiel; eigene Geräusche für Bär und Wolf (weiter „treffer“/„tod“).
**Noch offen aus der RT-Sitzung (nicht umgesetzt):** „Wölf und Bären leben meist in der Nähe von Wäldern und in Wäldern. Hier könntest du noch ein wenig optimieren“ (Raubtiere nahe am und im Wald aufstellen) und „Die Wälder würde ich noch ein weniger "luftiger" gestalten und die Abstände der Bäume zu einander etwa um 20% vergrössern“.

**Plätze löschen: der Spielstands-Platz lässt sich löschen (SV7)** (2026-10-23) — Wunsch: „alles drin ausser SV Spielstand-Plätze: Plätze löschen“ — die Listen von SV5/SV6 kannten nur Überschreiben; jetzt löst die Löschtaste auf einem Platz (Entf / Y am Gamepad, neu `InputState.IsMenuDelete`) sein Löschen aus: Ja/Nein-Bestätigung („Spielstand löschen? <Bezeichnung> — Dies lässt sich nicht rückgängig machen.“), bei Ja wird die Platzei gelöscht, die Liste baut an Ort und Stelle frisch auf (die Auswahl bleibt per `ClampSelectedEntry` in Reichweite) und ein Toast meldet „Spielstand gelöscht“; bei Fehler bleibt die Liste offen mit „Löschen fehlgeschlagen: …“. Store: neu die statische `SaveGameStore.DeleteSlot(path)` — Vertrag: nur Plattnamen (SLOT_NAME) **in** `SavesFolder`, sonst `ArgumentException` mit deutscher Meldung (außerhalb des Ordners oder Fremdnamen wie `einstellungen.json` bleiben heil), fehlende Datei gibt `FileNotFoundException`; die Nummer wird mit dem nächsten `NextSavePath` (kleinste freie) wieder frei — dokumentiert. Eintrag: `MenuEntry.Deletable` + `DeleteSelected`-Ereignis; `MenuScreen` leitet `IsMenuDelete` auf den gewählten Eintrag; gelöscht wird nur, was `Deletable` trägt („Neuer Spielstand“ und „Zurück“ bleiben immer an Ort und Stelle). Beide Menüs (`LoadGameScreen`, `SaveGameScreen`) markieren ihre Platz-Einträge als löschtogefähigt und rufen nach dem Löschen `NeuAufbauen()` auf (Liste ersetzen + Auswahl klammern, Vertragskommentare dazu). Tests: +4 `SaveSlotsTests` (Löschen und Nummer-wird-frei, außerorts bleibt heil + `ArgumentException`, Fremddame `ArgumentException`, fehlend `FileNotFoundException`) — sie laufen mit `AOE_SPEICHER_ORDNER` in einem TempOrdner, der echte Ordner neben den Einstellungen bleibt unberührt. **Abnahme:** `dotnet build` 0 Fehler · `dotnet test` **463/463** · Strukturprüfung `checks/sv_plaetze.py` (neu: `DeleteSlot`-Wache, Deletable/DeleteSelected in beiden Menüs, Klammer-Auswahl, `IsMenuDelete`) grün und **gegen den Altstand rot** (in Scratchpad-Kopie nachgewiesen) · Gruppe `tools/spielablauf menue speichern karten` auf 3 Karten grün · `tools/ai-pruefung` ok. **Offen:** Sichtprüfung durch den Nutzer im laufenden Spiel (Entf über einem Platz, Ja/Nein, Liste aktualisiert, „Spielstand gelöscht“).

**Kartengrößen umgebaut (C11, Folge)** (2026-10-23) — Wunsch: „die Kartengröße Standard löschen; der Standard soll die Karte Groß werden, Maximal heißt Kontinent, dazu eine größere World (nochmal doppelt so groß)“. Die frühere Standard 64×64 ist entfernt. Neue Stufen im Menü: **Standard 90×90** (die frühere Groß-Karte), **Kontinent 128×128** (die frühere Maximal-Karte), **World 181×181** (32 761 Kacheln, doppelte Fläche einer Kontinent-Karte — Fortsetzung der Ladder, die jede Stufe die doppelte Fläche hatte). `MapSize { Standard, Continent, World, Test }`; `MapSizes.Name` schreibt Standard/Kontinent/World. **Wichtig:** die Dichte-Referenz ist `MapSizes.BaseSide = 64` fixiert — `ForSize` rechnet relativ dazu, nicht relativ zur neuen Standard 90. Ohne den Fix hätte die neue Standard (90²) nur die halbe Ausbeute der alten Groß-Karte gehabt; so behalten Groß→Standard und Maximal→Kontinent exakt ihren bisherigen Karteninhalt, und World trägt die acht-fache Grundausstattung auf der acht-fachen Fläche (= doppelte Kontinent-Fläche, gleiche Dichte). Test 32² bleibt unverändert (Referenz 64, wie vor dem Umbau). **Migrationspfade (einmalig):** (1) `SaveGameStore.Deserialize` mappt alte Aufzählungs-Namen „Large“→Standard, „Max“→Continent, bevor das JSON geparst wird (die Namen sind serialisiert — ohne Mapping wären frühere Spielstände scheinbar kaputt); (2) Einstellungen: neues Feld `MapSizeVersion` — MainMenuScreen migriert bei `Version < 1` die Zahlen (alte 1 „Groß“ → 0 Standard, alte 2 „Maximal“ → 1 Kontinent, alte 0 „Standard“ → 0, da die 64² nicht mehr angeboten wird); die Version wird beim nächsten Größenwechsel mitgespeichert (keine Schreibzugriffe beim Menüstart), (3) CLI `--karte` akzeptiert jetzt `standard|kontinent|world`, die alten Namen `gross`/`max` heißen dieselben Karten wie heute. **Harnisch:** `tools/spielablauf` `Welt()` baut weiter die 64×64-Basisreferenz (unskalierte `MapSettings`) — alle Gruppen prüfen weiter ihre ursprünglichen Karteninhalte; `Welt(MapSize)` ist die neue Überladung für die Größenprüfung (Standard 90², Kontinent 128², World 181², je nach Format). **Änderungen:** MapSettings.cs (Enum, Side, Name, BaseSide, Dimensions(int,float), ForSize-Doku), SaveGameStore.cs (Name-Mapping), AgeOfEvolutionsSettings.cs (MapSizeVersion), MainMenuScreen.cs (Migration, Doku), AgeOfEvolutionsGame.cs (CLI), spielablauf (Welt-Überladung, karten-Gruppe, menue-Wechsel), kartenpruefung (Test aus dem Dichte-Vergleich — kein Produktformat), c11_karten.py, README. **Abnahme:** `dotnet build` (0 Fehler) · `dotnet test` · `tools/spielablauf -- karten menue` · `tools/kartenpruefung -- groessen` (Test-Karte ist kein Produktformat, ihre Dichte zählt nicht mit) · `python tools/ollama-agent/checks/c11_karten.py` · Breite Regression wie zuletzt. **Offen:** Sichtprüfung der drei Namen + der World-Karte im laufenden Spiel durch den Nutzer; nächster Release (nach v0.4.8) trägt den Umbau.

**Mauszeiger-Hervorhebung: das Klickziel wird leicht heller gezeichnet (C17)** (2026-10-23) — Wunsch: „man sieht nicht immer genau was man mit der Maus gerade auswählen würde. Also ob die Maus jetzt einen Baum auswählen würde oder den Dorfbewohner daneben. Heisst wenn die Maus über einem Objekt ist, müsste das Object leicht hervorgehoben werden. Zum Beispiel etwas heller gemacht werden.“ (Seit heute Regel: alle Programmieraufgaben selbst, kein qwen3.8:27b.)

Teildatei `Screens/RTSGameplayScreen.Hover.cs`. `HoverUnder(Weltpunkt)` prüft jeden Bild, was der Linksklick an der Stelle täte — **dieselbe Reihenfolge wie `LeftClick`**: Setzmodus → nichts; mit Auswahl ein sichtbarer Gegner → er; eigene Figur → sie; ohne Auswahl das Gebäude darunter; mit Auswahl das fremde Gebäude (Angriff); mit Auswahl und Dorfbewohnern das eigene beschädigte Gebäude (Reparatur, C15); mit Auswahl das lebende Raubtier auf der Kachel (RT); mit Dorfbewohner in der Auswahl die erforschte Rohstoffkachel mit Vorrat (dieselbe Bedingung wie `IssueCommand`). `HandleRtsInput` setzt daraus das Feld `_hover` — über der Leiste und im Setzmodus hebt nichts. Gezeichnet wird nicht als Rahmen, sondern **echtes Aufhellen**: ein zweiter Durchgang desselben Bilds im Additivton `HoverLift (40,40,40,0)` — Alpha 0 addiert im vormultiplizierten AlphaBlend (derselbe Trick wie das Schmiedefeuer A5) etwa ein Sechstel der Eigenfarbe darüber; die Umrisse bleiben exakt. Eingebunden in allen Zeichenschichten mit optionalem `bool hover`: Figuren (auch die Bild-Fallbacks), Gebäude (das stehende Bild; die wedelnde Fahne bleibt im Standbild — bei 40/255 unsichtbar anders), Bäume (mitgeschwungen, Streife für Streife), Beerenbüsche, Tiere und Fleisch, Stein- und Goldhaufen. Feldkacheln und Baugerüste heben noch nicht (dokumentierte Grenze).
Umgesetzt von Hand (Regel seit 2026-10-23: nie auf qwen3.8:27b wechseln, alles selbst).
Abnahme: Builds DesktopGL + WindowsDX 0 Fehler (die 2 Warnungen in EconomyAi sind vorbestehend) · `dotnet test` **458/458** · neue Gruppe `tools/spielablauf hover` (acht Abläufe je 3 Karten: eigene Figur auch ohne Auswahl, Gegner nur mit Auswahl, Gebäude ohne / fremdes mit Auswahl, Reparatur nur mit Dorfbewohner, Baum nur mit Dorfbewohner (Soldaten: None), Raubtier nur mit Auswahl, Setzmodus hebt nichts, `HandleRtsInput` pflegt `_hover` (Klick auf die Figur, über der Leiste nichts); dreimal hintereinander grün) · neue Strukturprüfung `tools/ollama-agent/checks/c17_hover.py` (Vertragsbausteine, Additivton Alpha 0, alle sechs Einbindungsstellen) grün und **gegen den Altstand rot** (in Scratchpad-Kopie nachgewiesen) · breite Regression `linksklick auswahl ziele soldaten angriff verteidigung kampfbild raubtiere wild schafe tuer strassen neubauten bauen weiterbauen bauphasen pfeile farm beweg zielwechsel schatten` auf 3 Karten grün (`autoantwort` dabei einmal von vornherein bekannt zufällig rot — im RT-Eintrag dokumentiert, keine Bewegung im Code berührt) · Startprobe GL + DX. **Offen:** Sichtprüfung durch den Nutzer im laufenden Spiel (Schwebe über Figur, Baum, Haufen, Gebäude).

**Raubtiere: Bären und Wölfe greifen an, wer ihnen zu nahe kommt (RT)** (2026-10-23) — Wunsch: „Ich würde gerne noch zwei weitere Wildtiere im Spiel einfügen. Bären und Wölfe in hoher qualität. Kommen Dorfbewohner oder Soldaten den Tieren zu nahe werden sie von ihnen angegriffen. Wie Wildschweine (nur seltener) laufen sie herum aber von Gebäuden halten sie Abstand (mindestens 4-5 Kacheln). Benutze die Lokalen model für die Erstellung“ und „Sie verfolgen aber dann keine Menschen wenn man davon läuft nur wenn man zu nahe zu Ihnen hinläuft“.

Kern (`Data/TileMap.Raubtiere.cs`, `TileMap` ist jetzt `partial`): Bär und Wolf sind Wild wie die Wildschweine (`FoodSource.Bear`/`Wolf`, `IsWild`, neu `IsPredator`), das `WildAnimal` trägt dazu `Health`, `AttackCooldown` und `Strike` (Angriffsbild). Lebend trägt ihre Kachel keine Nahrung — man muss sie erst erlegen. Karte: 2–3 Bären (Einzelgänger) und 2–3 Rudel zu 2–3 Wölfen (`MapSettings`, wachsen mit der Kartenfläche), aufgestellt nach den Stadtzentren (`PlacePredators` am Ende von `InitializeRandomMap`), mindestens 24 Kacheln (Manhattan, `PredatorStartDistance` 12) von beiden Startplätzen — weiter weg als die Start-Rehe — und 5 Kacheln von jedem Gebäude. `UpdatePredators` (jedes Bild aus `UpdateUnits`): steht eine lebende Einheit, egal welches Spielers, höchstens `PredatorReachTiles` 1,25 Kacheln von der Kachelmitte, schlägt das Tier die nächste (Bär 6 Schaden alle 2 s, Wolf 3 alle 1,5 s, minus Rüstung, mindestens 1), blickt zu ihr und wandert nicht; mitten im Schritt schlägt es nicht. **Es verfolgt niemanden** — wer weggeht, ist sicher. Ohne Opfer wandert es seltener als ein Wildschwein (Bär 6–12 s, Wolf 4–9 s, Wildschwein 2–5 s) und nie auf eine Kachel näher als `PredatorBuildingDistance` 5 (Chebyshev zur nächsten Gebäudekachel, auch Baustellen beider Spieler); steht es zu nah (ein neues Gebäude), zieht es sich Schritt für Schritt auf dem kürzesten Weg nach draußen zurück (Breitensuche bis 12 Kacheln, um Wald und Felsen herum). Erlegen (`DamagePredator`): Bär 80 LP → liegt als Fleisch da (400 Nahrung), Wolf 20 LP → verschwindet. Spielstand: `Health` und `AttackCooldown` im `AnimalSave`.
Spielbildschirm (`Screens/RTSGameplayScreen.Raubtiere.cs`): jeder Schlag klingt („treffer“) und löst die Angriffswarnung aus; ein getroffener **Soldat** ohne eigenes Ziel wehrt sich (`AttackAnimal`), Dorfbewohner arbeiten weiter. Klick mit Auswahl auf ein sichtbares Raubtier (auch auf die Kachel darüber oder daneben — das Bild ragt hinein) = Angriffsbefehl (`Unit.PreyTarget`); Nahkämpfer treten auf 0,75 Kacheln heran (und werden gebissen), Schützen schießen aus ihrer Bogenvorlage (`ArrowTarget`) und bleiben unbehelligt. Ist der Bär erlegt, zerlegen ihn die beteiligten Dorfbewohner gleich (`PreyDone`), wie Jäger in AoE. Schlagbilder und Blick wie gegen Einheiten (`IsStriking`, `UpdateUnitMotion`).
Bilder (Qwen-Image-2512 lokal über ComfyUI, `tools/bilder` Gruppen `tiere`, `tiere_lauf`, `fleisch`): je zwei Bären (stehend; schnüffelnd mit gesenktem Kopf) und Wölfe (grau, stehend; dunkel, schleichend), je mit zwei Laufbildern, einem Angriffsbild (Bär brüllend mit erhobener Pranke, Wolf zähnefletschend) und das Fleisch des Bären — in 256 statt 128 px Breite. Gezeichnet mit `BEAR_WIDTH` 1,3 und `WOLF_WIDTH` 1,0 Kacheln, Lauf- und Angriffsbild wie beim übrigen Wild, Schlagschatten wie alle Tiere, roter Lebensbalken über einem verwundeten Raubtier, ein erlegter Wolf sinkt in 0,4 s zu Boden und verblasst in 1,2 s. `qwen_image.py` kann Masken jetzt weich auslaufen lassen (`"weich"`): harte Rechteckkanten hinterließen Nähte und abgeschnittene Beine.
Umgesetzt vom lokalen Modell (`qwen3.8:27b`) nach Vertrag in sechs Aufgaben, je 16–41 s: RT1a (einfache Kernrümpfe, Spielstand), RT3a (Zeichenrümpfe) und RT3b (Einbindung der Bilder) im ersten Lauf angenommen. RT1b scheiterte zweimal am Build (erst `ContainsKey(px, py)` ohne Tupelklammern und `nx`/`ny` doppelt, nach der Rückmeldung eine in einem `if`-Block deklarierte Variable `center`) — die sonst vertragstreue zweite Fassung **von Hand** um eine Zeile korrigiert (Deklaration vor den Block). RT2a scheiterte erst an `GetTile(...).Resource` statt `ResourceType`, im zweiten Lauf nur an den Tests, die RT1b brauchten; RT2b ebenso nur daran — beide abgelehnten Fassungen eingespielt und mit ihren eigenen Verify-Listen abgenommen (Lehre: Verify-Listen nicht an noch offene Aufgaben hängen). Einbindung, Datentypen, Spielstand und `.mgcb` zeichengleich mit der Referenzumsetzung.
Abnahme: Builds DesktopGL + WindowsDX 0 Fehler · `dotnet test` **458/458** (+22 `RaubtiereTests`: Aufstellen, Gebäudeabstand, Zufallskarten fern der Startplätze und Gebäude, sofortiger Schlag in Reichweite, Takt je Art, Abstand halten = unbehelligt, **kein Verfolgen**, nächstes Opfer, Tote nicht, tödlicher Schlag, kein Schlag mitten im Schritt, Rückzug vom neuen Haus auch um einen Waldwinkel herum, Wandern nie in die Zone, Bär → Fleisch, Wolf verschwindet, `LocatePredator`, Spielstand, `RebuildWildAnimals`) · neue Gruppe `tools/spielablauf raubtiere` (annähern und weggehen, Gegenwehr der Miliz gegen einen Wolf, Jagd per Klick mit fünf Dorfbewohnern bis zum Zerlegen, Bogenschütze aus der Ferne unverletzt, Rückzug vor einem neuen Haus) auf **24 Karten in Folge** grün · `angriff zielen verteidigung kampfbild zielwechsel kampfklang wild schafe speichern pfeile schatten` und ein breiter Lauf über 38 weitere Gruppen grün — die Prüfwelt räumt dafür die zufälligen Raubtiere ab; rot bleiben wie vorher `karten` (erwartet Stadtzentren in den Ecken, seit SZ1 überholt) und zufällig `autoantwort` („Karte zu klein für die Mitte“) · `tools/ai-pruefung` (KI gegen KI, `gegenwehr`, `entwicklung 25`) mit Raubtieren auf der Karte ok · `tools/kartenpruefung wild start fisch` · Mutantenlauf 23 von 23 gefunden (u. a. Verfolgen statt Stehenbleiben, doppelte Reichweite, Wandern in die Gebäudezone, keine Gegenwehr, Dorfbewohner wehren sich, Klick nur auf die Kachel, Schütze läuft heran) · `checks/rt_raubtiere.py` (Einbindung, Content), `u_vertrag.py` · Startprobe GL + DX · Fotos im Spiel: Prüfbogen aller 16 Zustände (Stand, Lauf 1, Lauf 2, Angriff je Bär, Bär 2, Wolf, Wolf 2) pixelgleich mit der Referenz, dazu Fleisch, Lebensbalken, umsinkender Wolf und eine Kampfszene. **Offen:** Sichtprüfung durch den Nutzer; eigene Geräusche für Bär und Wolf (bisher „treffer“ und „tod“).

Davor: **Zufällige Startplätze: das Stadtzentrum steht nicht mehr fest in der Ecke (SZ1)** (2026-10-22) — Wunsch: „ich würde gerne zuerst sicherstellen das der Spieler nicht immer in der Linken oberen Ecke anfängt. Das Dorfzentrum soll irgendwo per zufahl hingestellt werden und mindest Abstand zum Gegner haben der die Hälfte der Kartenbreite entspricht".

`TileMap` würfelt beide Startplätze zuerst (`ChooseStartPositions`, vor `AddResourcePatches`): beide unabhängig gleichverteilt in der Randzone x 3…Width-7, y 3…Height-7 (darin passen `ClearStartArea` bis +5 und die Dorfbewohner-Versätze), euklidischer Abstand mindestens die halbe Kartenbreite (`Width / 2,0`), bis 400 Würfe, bei sehr kleinen Karten die gegenüberliegenden Ecken des Bereichs. `StartPosition(owner)` liefert die Plätze nach außen. Alle bisherigen Ecken-Annahmen lesen die Felder `_startP1`/`_startP2`: `NearStartArea` (Seen meidet die Mitten beider Stadtzentren), `TooCloseToStart` (neu symmetrisch, je Manhattan `dist * 2`), `PlaceStartResources` (Stein und Gold zur Kartenmitte hin, Richtung je Achse aus der Lage zur Mitte), `PlaceDeer` (je eine Startjagd-Herde), `PlaceStartingPositions` (Stadtzentren; Dorfbewohner-Versätze gleich). Der Startblick der Kamera (`RTSGameplayScreen.LoadContent`) folgt `StartPosition(0)` statt (3, 3). Die leere Karte (`CreateBlank`) behält (3, 3)/(0, 0) - dort kommen die Stadtzentren aus dem Spielstand.
Umgesetzt von Hand (die GPU war mit 30,7/32,6 GiB belegt, Ollama hätte kein qwen3.8:27b geladen; Nutzer: „du kannst das direkt selber übernehmen"). Die Aufgabe SZ1 steht mit Vertrag und Abnahme in `tasks.json` bereit.
Prüfwerkzeuge von den Ecken gelöst: `tools/spielablauf` `Welt.Bauplatz`/`Welt.FreieKachel` tasten jetzt die ganze Karte ab (vorher 0..39 - bei einem Start rechts unten fand sich kein einziger Bauplatz), `FarmErnte` verlangt für die angeklickte Farm-Mittelkachel `IsTileExplored` (ein Klick in den Nebel war ein Laufbefehl ohne Ernte). Neue Kartenprüfgruppe `startabstand` (Abstand ≥ halbe Breite, Randzone, nicht immer derselbe Platz); neue Strukturprüfung `checks/z1_startplaetze.py` (kein Eck-Anker mehr in den Start-Methoden, Reihenfolge Würfelung vor Klumpen, Kamera folgt).
Abnahme: Builds DesktopGL + WindowsDX 0 Fehler · `dotnet test` **436/436** (+4 `StartplaetzeTests`: Abstand ≥ halbe Breite auf fünf Kartenformaten je 20 Karten, Randzone, Stadtzentrum + vier Dorfbewohner am Platz, über 40 Karten nie derselbe Platz) · `tools/kartenpruefung startabstand start wild beeren fisch` grün — Abstand im Schnitt 42,0, kleinster 32,7 (gefragt ≥ 32), Spieler 1 startet auf 50 von 50 verschiedenen Plätzen, Stein/Gold bleiben in Laufweite (höchstens 10 Kacheln) · `tools/spielablauf strassen wegkarte gebaeudewege nebelboden festerboden pfad bauen weiterbauen bauphasen zeitalter speichern tuer turm neubauten reparatur dunkel animation pfeile farm` auf 3 Karten grün · `checks/z1_startplaetze.py` grün (vorher korrekt rot gegen den Altstand) · Startprobe gl. **Offen:** Sichtprüfung im Spiel (Kamera startet beim eigenen Stadtzentrum, der Gegner ist mindestens eine halbe Kartenbreite weg).

Davor: **Mehrere Spielstand-Plätze: Laden und Speichern als Listen (SV5, SV6)** (2026-10-22) — Wunsch: „I can already save a game. But I want to save many games. So the menü needs also to show all possible load points.“

Plätze sind Dateien im bisherigen Ordner `%APPDATA%\AgeOfEvolutions\`: der erste heißt weiter `spielstand.json` (der Altstand bleibt sichtbar und überschreibbar), jeder weitere `spielstand-2.json`, `-3.json` … die kleinste freie Nummer ab 2. `SaveGameStore` bekommt die Platz-Statik: `SavesFolder` (mit Öffnung `AOE_SPEICHER_ORDNER` für Prüfungen, wie `AOE_SPIELSTAND_DATEI`), `ReadHeader` (liest mit `JsonDocument` nur Speicherzeit, Spieldauer und Zeitalter — die Welt wird für die Liste nicht aufgebaut; die weggesparten Standardwerte `GameSeconds` 0, `Ages.Current` Dark und `Id` 0 sind eingearbeitet), `ListSaves` (nur `spielstand(-Zahl).json`, kaputte Stände übersprungen, neuester zuerst) und `NextSavePath`; `SaveSlot.Label` zeigt „22.10.2026 21:14 · Ritterzeit · 37 min“ (Mittelhochpunkt 183 ist in der Menüschrift 32–254). **Hauptmenü:** „Spiel laden“ öffnet die neue Liste `LoadGameScreen` (Eintrag je Platz + „Zurück“; Klick lädt — `SaveGameMapper.Restore` weiter als Probe —, ein kaputter Stand meldet „Spielstand nicht lesbar“ und die Liste bleibt offen); die Ladeliste gehört zum Hauptmenü, das Pausenmenü speichert nur. **Pausenmenü:** „Spiel speichern“ öffnet `SaveGameScreen`: „Neuer Spielstand“, je Platz einer zum Überschreiben, „Zurück“; gemeldet wird „Spiel gespeichert“ (Liste schließt, Meldung bleibt 1,5 s) oder der Fehlergrund. `--laden` unverändert (Standardort wie bisher).
Umgesetzt von Hand: die Aufgaben SV5 und SV6 standen mit Verträgen, Rümpfen und Abnahme wie immer im `tasks.json` bereit, aber der lokale Agent konnte `qwen3.8:27b` in diesem Fenster nicht laden — die GPU war mit 31/32,6 GiB fremd belegt, Ollama meldete OOM. Die Rumpfumsetzung darum selbst übernommen; abgenommen ist sie über genau die Verify-Listen der beiden Aufgaben. `_cs.py`-Korrektur beim Prüfen: der Methoden-Finder übersah jetzt auch Konstruktoren mit `: base(...)`-Initializer nicht mehr (Falle 7-Verwandter).
Abnahme: Builds DesktopGL + WindowsDX 0 Fehler · `dotnet test` **426/426** (+14 `SaveSlotsTests`: Kopf-Lesung samt Standardwerten und Kaputtheit, Reihenfolge, Fremd-`.tmp`/Ordner/Fremdnamen ignoriert, Groß-/Kleinschreibung, Nummerierung ab 2 ab der kleinsten Lücke, `Label`) · Gruppe `menue` auf die Listen und `AOE_SPEICHER_ORDNER` in %TEMP% umgestellt, mit echtem Mini-Spielstand statt `{}` · `checks/sv_menue.py` der neuen Einbindung angepasst · neue Strukturprüfung `checks/sv_plaetze.py` (Rümpfe gefüllt, beide Listen rufen Vertrag und Menüs öffnen sie) · `sv_laden.py gl|dx` und `start_rts.py gl|dx` grün. **Offen:** Sichtprüfung der beiden Listen durch den Nutzer. (Plätze löschen: oben eigener Eintrag, SV7 — ab 2026-10-23 umgesetzt.)

Davor: **Schlagschatten wie Sonnenschatten (C7)** (2026-10-22) — Wünsche: Schatten „brauch es auch“; dann zu den ersten Ovalen „die Schatten müssen schon alle in die gleiche Richtung zeigen“ und „sie haben die falsche Form“, „sie dürfen keinen Abstand haben“, „prüf doch mal wie reale schatten wirklich geworfen werden“, zu den Bäumen „da geht noch mehr“, zu Gebäuden und Haufen „komplett eine andere Form“ und „nur einen Strich als Schatten“; Vorbild `errors/Schatten.jpg` (Quader in der Sonne: Licht von links hinten, Schatten nach rechts vorn); dann ein Schattenkeil nach oben an einer Dachtraufe (`errors/rot markierter fehler.png`, „diesen Fehler hat fast jedes Gebäude“) und „mach die Schatten kürzer etwa halb so lang“; zuletzt ein unten gerade abgeschnittener Kronenschatten (`errors/Schatten ist hier scharf abgeschnitten.png`).

Teildatei `Screens/RTSGameplayScreen.Schatten.cs`. Jedes stehende Objekt wirft seine eigene Silhouette, nach den Regeln echter Sonnenschatten: **parallele Strahlen** - alle Schatten fallen nach rechts vorn (Licht von links hinten, die Qwen-Bilder sind links hell; `SHADOW_SKEW` 0,38 und `SHADOW_FLATTEN` 0,175 je Bildpunkt Höhe, Sonne etwa 67° hoch) und liegen so ganz sichtbar vor dem Objekt; **am Bodenkontakt** - Gebäude und Haufen je Spalte von ihrer untersten deckenden Zeile aus (jede Wand steht auf ihrer eigenen Unterkante, der äußere Rand ist der Umriss) - die Bodenlinie steigt vom tiefsten Punkt nach außen höchstens einen Punkt je Spalte, so stehen Dachüberstände, Fahnentücher und Erker am Rand auf der Wand statt in der Luft -, die Schattenpunkte benachbarter Spalten werden verbunden, damit der Schatten bei schräger Unterkante geschlossen statt schraffiert ist; Bäume, Büsche, Figuren und Tiere als Körper: jede Bildzeile eine waagerechte Ellipse (Tiefe `SHADOW_DEPTH` 0,5 der Breite) vom tiefsten Punkt aus - so wirft eine Krone einen runden Fleck, mit dem Stamm am Fuß verbunden; **Kernschatten ohne Doppeltdunkel** - alle Masken kommen mit Maximum (`ShadowUnion`) in ein eigenes Render-Target, das einmal über den Boden gelegt wird (`BeginCastShadows`/`EndCastShadows` um `DrawTileMap` und `DrawUnits`), der Bildpuffer behält dafür seinen Inhalt (`RenderTargetUsage.PreserveContents` in `AgeOfEvolutionsGame`); **scharfer Rand** (`SHADOW_SOFT` 1); **kühler Schatten** - im Schatten bleibt das blaue Himmelslicht (`SHADOW_TINT`, die Hälfte des Lichts fehlt), Laub lässt Licht durch (`TREE_SHADOW_DENSITY` 0,85). Die Masken entstehen einmal je Bild in halber Auflösung (`HalfAlpha`, `BakeShadowMask`, `ShadowMaskFor`). Felder sind flach und werfen keinen Schatten mehr; die Ovale (`_shadowTex`, `BuildShadowTexture`) sind weg.
Umgesetzt vom lokalen Modell nach Vertrag in neun Aufgaben (C7sch3a–f, C7sch4, C7sch5, C7sch6, je 10–30 s; C7sch5 - Glättung der Bodenlinie - im ersten Lauf; C7sch6 - die Maske ist unten um die halbe Tiefe der breitesten Krone und den weichen Rand höher - im zweiten, den ersten hatte meine eigene, veraltete Strukturprüfung abgelehnt): 3b erst im zweiten Lauf (LINQ ohne `using System.Linq`), 3f bessert eine Abweichung in `HalfAlpha` nach (bei ungerader Zahl verdeckter Zeilen fiel eine Zeile weg - nur für Bauphasen; der Test prüfte zuerst nur eine gerade Zahl), C7sch4 dreht die Richtung nach vorn: im ersten Lauf nahm das Modell die oberste statt der untersten Zeile als Fuß, im zweiten blieb nur CS1628 (out-Parameter in einer lokalen Funktion) - **von Hand behoben** mit lokalen Kopien `sw`, `sh`. Einbindung zeichengleich mit der Referenz. Davor verworfen: zwei Fassungen mit Ovalen (C7sch, C7sch2) und eine nach rechts hinten (lag großteils hinter dem Gebäude, übrig blieben Striche).
Abnahme: Builds DesktopGL + WindowsDX 0 Fehler, 0 Warnungen · `dotnet test` **412/412** · Gruppe `tools/spielablauf schatten` neu (Maske Punkt für Punkt gegen eine zweite Umsetzung des Vertrags, 10 Formen je mit Kontur und als Körper; Schatten hängt am Fuß, fällt nach rechts vorn; Deckkraft 128/127; Block nicht dunkler als 255; `HalfAlpha` mit Spiegelung und verdeckter Oberkante, auch ungerade; `ShadowDest`) mit `farm feld pfeile bauphasen feuer` grün · Strukturprüfung `checks/c7_schatten.py` in fünf Stufen (Grafikrümpfe nach Vertrag, jeder Schatten direkt vor seinem Bild, keine Ovale mehr) · `u_vertrag.py` · Mutantenläufe 5 von 5 und 4 von 4 gefunden; neuer Fall „Dachüberstand“ (rot ohne Glättung) · Startprobe GL + DX · Fotos im Spiel mit und ohne Schatten · Prüfbogen mit jedem Gebäudebild aller vier Zeitalter, Haufen, Bäumen und Figuren samt Schatten (Nachrechnung in Python) vor der Abnahme durchgesehen. **Grenze:** Figuren und Grasbüschel im Schatten bleiben hell (sie werden nach dem Schattenbild gezeichnet) - nächster Schritt wäre ein Effekt, der sie am Schattenbild abdunkelt.

Davor: **Türweg: die Straße läuft unter dem Gebäude bis an die Tür (T16)** (2026-10-21) — Wunsch: die Straße hält an der Unterkante des Gebäudes an; sie soll noch unter dem Gebäude hindurch bis an die Tür weiterlaufen.

`RTSGameplayScreen`: `DoorColumnOf` (Türspalte = Türanteil der Bauwerksbreite, geclampt auf die Gebäudeweite — `DoorRoadOf` teilt sich jetzt diese Stelle, Türort bleibt gleich) und neu `ConnectDoorRoad` (Vertrag: Mauern, unfertige Gebäude, Dunkle Zeit, keine Wege oder schon Weg an der Tür → nichts; sonst in der Tür-Reihe `b.Y + b.Height` den nächsten Weg in höchstens `DOOR_ROAD_MAX` = 12 Spalten suchen und die Lücke Kachel für Kachel zur Tür ausgleichen — nur über `CanCarryRoad`-Kacheln (Gold, Feld, Gebäude unterbrechen), Wegstufe = max(aktuelle Stufe, Zeitalterstufe); Rückgabe = gezählte Kacheln). Eingehängt in `ConnectToRoads` (Gebäude-Vollendung und Zeitaufstieg) hinter `ConnectBuilding`. Zeichenseite: `DoorRoadRects()` (pure Logik ohne Grafiken, `tools/spielablauf`-tauglich: je Gebäude mit angelegter Tür ein Rechteck in der Türspalte von der Unterkante bis zur Türschwelle, Bandbreite der erreichten Wegstufe: Stufe 1 = 0,24 / Stufe 2 = 0,27 / Stufe ≥ 3 = 0,30 der Kachelkante) und `DrawDoorRoad` (eigener Durchgang nach `DrawRoads`, nur bei Wege: Weg-Texturstück mit Quell-/Zielrechteck im `ROAD_TEXELS`-Maßstab des Shaders — der Kopf setzt sich nahtlos fort — gefärbt wie `DrawRoads`; danach `ReleaseTextureUnits`).
Abnahme: `dotnet build AgeOfEvolutions.slnx` 0 Fehler, keine neuen Warnungen (alle Warnungen in Testdateien, vorbestehend) · `dotnet test` **412/412** · neue Gruppe `tools/spielablauf tuerweg` (8 Pröbungen × 3 Karten: Dunkle Zeit = keine Türwege; Feudalzeit ohne Weg = nichts passiert; Lücke 4 Spalten = 4 Kacheln ausgeglichen, Türkachel gezeichnet, Stufe 1, Türpunkt + Tiefe, Türwege-Rechteck in Türspalte an der Unterkante; Ritterzeit = Ausgleich auf Stufe 2; Palisadenmauer = kein Türweg; Tür schon angebunden = nichts passiert; Distance = 12 Spalten ausgeglichen, 13 nicht; Goldblockade = Halt vor der Goldkachel) grün · `tools/spielablauf strassen wegkarte gebaeudewege nebelboden festerboden pfad bauen weiterbauen bauphasen zeitalter speichern tuer turm neubauten reparatur dunkel animation pfeile farm` auf 3 Karten grün · `tools/ai-pruefung` alle Prüfungen ok (beide KI-Instanzen) · Mutantenlauf 2 von 2 gefunden (`DOOR_ROAD_MAX` 12→5 bricht Distance 12; ohne `CanCarryRoad`-Gate läuft die Goldblockade durch) · `checks/u_vertrag.py` mit `DoorColumnOf ConnectDoorRoad DoorRoadRects DrawDoorRoad` grün · Startprobe GL + DX (je 10 s ohne Absturz). **Offen:** visuelle Sichtprüfung durch den Nutzer (Screenshot mit Türweg im Spiel) — die Headless-Tools zeichnen keine Frames; die Änderung liegt un-committed im Arbeitsbaum.

Davor: **Feuer in der Schmiede und Kaminrauch, je Haus und Schmiede genau ein Kamin (A5, C6f)** (2026-10-20) — Wünsche: „die Animation für die Feuer in der Schmiede fehlt. So wie der Kaminrauch von den häusern“, dann „achte darauf das jedes gebäude nur einen Kamin hat“ und „wo nötig musst du das Bild neu generieren mit einem Kamiin“.

Neuer Abschnitt „A5 Feuer und Rauch“ in `Screens/RTSGameplayScreen.Animationen.cs`, alles zustandslos aus `animationTime` (nichts im Spielstand). **Feuer** (`DrawForgeFire`): über dem gemalten Feuer ein flackernder Lichtschein (`FireFlicker`, drei Sinus in krummen Takten), fünf Flammenzungen (`FlameTongue`, Bild `FlameTexture`: unten breit, oben spitz; außen orange, innen gelb, jede im eigenen Takt, außen kürzer und nach außen geneigt) und sechs Funken (`Spark`), die aufglimmen, steigen und verlöschen. Alle Farben mit Alpha 0 — im vormultiplizierten AlphaBlend des Batches addieren sie sich, das Feuer leuchtet ohne eigenen Batch und bleibt in der Tiefensortierung. Feuerstelle je Bild gemessen (`ForgeFires`: Fuß, Breite, Höhe der gemalten Flammen). **Rauch** (`DrawChimneySmoke`): je Kamin zehn weiche Wolken (`SmokePuff`, `PuffTexture`), gestaffelt alle 0,5 s, 5 s Aufstieg über 220 Bildpunkte; sie wachsen, werden heller und verwehen, der Wind (`Wind.Gust` am Gebäude, dieselben Böen wie Bäume und Weizen) treibt sie nach rechts. Kaminöffnung je Bild gemessen (`ChimneyTops`, ein `Vector2` je Bild). Baustellen brennen und rauchen nicht (nur der fertige Zweig von `DrawBuilding` ruft beides).
**Ein Kamin je Gebäude, Bilder ausgebessert** (Qwen-Image-Inpainting, nur die Maskenfläche neu gemalt, Fahne und Tür bleiben pixelgleich): Häuser der Dunklen Zeit (`haus_kamin` Seed 5091, Feldsteinkamin im Strohdach), Feudalzeit (5191, Feldsteinkamin auf den Schindeln) und Imperialzeit (5357, heller Steinkamin mit dunkler Kappe neben der Gaube — Qwen malte auf der engen Schieferfläche meist gar keinen Kamin oder zerschnitt den First, 11 Seeds versucht); die Imperial-Schmiede (`schmiede_ein_kamin` 5397) hat ihren zweiten, linken Kamin verloren. Die alten Einträge stehen ohne `ziel` weiter in `bilder.json`. Haus-Sprites behalten ihre Größe; die Imperial-Schmiede ist oben 2 px kürzer (371 statt 373), ihr `FlagCloth` rückt von y 92 auf 90, ihre Feuerstelle von 268 auf 266. Ritterzeit-Haus und Feudal-/Ritter-Schmiede hatten schon genau einen Kamin.
Umgesetzt vom lokalen Modell nach Vertrag (A5, 4 Züge, 45 s, im ersten Lauf angenommen; Code zeichengleich mit der Referenz, nur Kommentare anders — „außen rot“ von Hand zu „orange“ berichtigt).
Abnahme: Builds DesktopGL + WindowsDX 0 Fehler · `dotnet test` **412/412** · neue Gruppe `tools/spielablauf feuer` (je Haus und Schmiede genau ein Kamin und das Bild dazu, jede Schmiede eine Feuerstelle; `SmokePuff`, `FireFlicker`, `FlameTongue`, `Spark` genau nach Vertrag; Rauch und Funken stetig von Bild zu Bild, Wolken gestaffelt, Flackern zwischen 0,15 und 0,99) mit `pfeile einsturz bauphasen fahne` grün · neue Strukturprüfung `checks/a5_feuer.py` (Einbindung nach dem Windrad, additive Farben, Bausteine der Zeichenrümpfe), `u_vertrag.py`, `c6a_bewegt.py` (Fahnenrechtecke passen zu den neuen Bildern), `c4g_zeitalter.py` (prüft die Pflichtbilder jetzt nach `ziel` statt nach Namen) · Mutantenlauf 18 von 18 gefunden · Startprobe GL + DX · Fotos im Spiel (Foto-Patch in der Scratchpad-Kopie, alle vier Zeitalter, drei Zeitpunkte): Rauch steigt genau aus jedem Kamin, das Feuer wechselt Form und Helligkeit, Funken steigen. Offen: Sichtprüfung durch den Nutzer.

Davor: **Pfeilbilder: einfacher Pfeil und „Befiederte Pfeile“ (A4)** (2026-10-20) — Wunsch: „kannst du danach noch die Pfeile in high quality generieren. Die zwei verschiedenen typen. Also nach der Weiterentwicklung müssen sie ja etwas anders aussehen“.

Die Weiterentwicklung ist die Schmiede-Forschung „Befiederte Pfeile“ (`Tech.Fletching`). Neue Bildgruppe `pfeile` in `tools/bilder/bilder.json` (Qwen-Image 1664×928, waagrecht, Spitze rechts, freigestellt, 128 px breit), je 3 Seeds erzeugt und gewählt: `Content/Effekte/pfeil.png` (Seed 10102: heller Holzschaft, dunkle Eisenspitze, graubraune Federn) und `Content/Effekte/pfeil_befiedert.png` (Seed 10201: Eschenschaft, lange Stahlspitze, rot-weiße Federn mit roter Wicklung), beide in `AgeOfEvolutions.mgcb` (Mipmaps). `Arrow.Fletched` hält beim Abschuss fest, ob der **Besitzer des Schützen** die Forschung hat (`PlayerOf(unit).Techs`); `DrawArrows` zeichnet das passende Bild um den Flugwinkel gedreht und auf `ArrowLength` skaliert (einfach 14, befiedert 17 Weltpixel); fehlt ein Bild, die Striche aus A3 (Federn graubraun bzw. rot). Umgesetzt vom lokalen Modell (A4, 26 s, Code bis auf Kommentare gleich der Referenz).
Abnahme: Builds DesktopGL + WindowsDX 0 Fehler · `dotnet test` **412/412** · `tools/spielablauf pfeile einsturz bauphasen kampfklang zielen verteidigung soldaten angriff kampfbild` auf 3 Karten grün — `pfeile` prüft neu fünf Fälle (ohne Forschung, Spieler 0 bzw. 1 hat sie, je eigener und fremder Schütze) und die Länge; Mutanten „immer Spieler 1“, „immer/nie befiedert“, „gleiche Länge“ alle gefunden · `checks/u_vertrag.py` (ArrowLength, DrawArrows), `start_rts.py gl|dx` · Foto im Spiel (Foto-Patch in der Scratchpad-Kopie, Zoom 4): beide Typen im Flug steigend, waagrecht, fallend klar zu unterscheiden. Der einfache Pfeil ist auf dunklem Gras eher unauffällig (wie ein echter Holzpfeil).

**Update-System repariert: „Update“ als gewöhnlicher Menüeintrag, das Update ersetzt die laufende Installation an Ort und Stelle** (2026-10-20) — Wünsche: „ich glaube das updaten funktioniert noch nicht ganz wie gedacht. Siehe im errors ordner Update in menu.png“, dann „erstens ist es nicht wie die anderen menüs und zweitens scheint das update nicht richtig zu funktionieren“.

Ursache 1, Aussehen: die Update-Zeile war eine eigene, flache Platte mit 24-px-Text, der lange Angebotstext ragte über Platte und Tafel hinaus. Ursache 2, Ablauf (aus den Logs in `release/publish/v0.4.4/win-x64-windowsdx/`): der Klick lud v0.4.5, aber die **DesktopGL**-Zip (`PlatformZip` kannte unter Windows nur desktopgl) und entpackte sie in den Unterordner `AoE_Evolutions_v0.4.5/win-x64-desktopgl/` der laufenden **WindowsDX**-Installation und startete sie dort; die Installation selbst blieb 0.4.4, beim nächsten Start bot sie das Update erneut an. Dazu: `DefaultDownloadFile` rief `GetAsync` ohne `ResponseHeadersRead` — die ganze Zip lag erst im Speicher, der Fortschritt stand bis zum Schluss auf 0; die alte Version schloss sich nur per weiterem Klick; die Stream-Kopie beim Entpacken verlöre unter macOS/Linux das Ausführungsrecht.
Neu (`UpdateService`): Download nach `<Install>/update/`, SHA256, Entpacken nach `update/neu/`, `StagedRoot` (Top-Ordner der Zip), dann `ReplaceInstallation`: jede alte Datei wird zu `<Name>.update-alt` umbenannt und die neue an ihren Platz verschoben — Windows verbietet Überschreiben und Löschen laufender Exe/DLL, erlaubt aber das Umbenennen (an einer laufenden v0.4.4 nachgemessen: 231 von 231 Dateien, auch die 70 geladenen); scheitert ein Schritt, wird alles zurückgesichert. Danach startet die neue Exe aus demselben Ordner, das Hauptmenü beendet die alte genau einmal regulär (`ExitAfterUpdate` → `Game.Exit()`), der nächste Start räumt `*.update-alt*` und `update/` weg (`CleanupAfterUpdate`, bis 20 Versuche à 0,5 s, solange die alte noch endet). Unter Windows kommt die Zip der laufenden Variante (`WindowsDX` = `SharpDX.Direct3D11.dll` liegt neben der Exe). Spielordner ohne Schreibrecht → Browser auf die Download-Seite. Download mit `ResponseHeadersRead` und 15-min-Frist über Kopf und Inhalt; `ExtractZip` per `ExtractToFile` (Unix-Rechte). Menü (`MainMenuScreen`): „Update auf v0.4.5“ ist ein gewöhnlicher Eintrag unter „Beenden“ (gleiche Platte, Maus, Pfeile, Enter), nur mit Angebot sichtbar; während des Laufs „Lade v0.4.5 … 37 %“ mit Balken in der Platte, „Neues Spiel“ und „Spiel laden“ sind dann gesperrt; darunter die kleine Versionszeile (`VersionLine`, lange Fehlermeldungen werden kleiner gezeichnet). Umgesetzt vom lokalen Modell nach Vertrag: U1a (40 s), U1b (zweiter Lauf, 44 s — der erste scheiterte an `Task.Run` ohne `using System.Threading.Tasks`), U2a (16 s), U2b (22 s). Nachbesserung aus U1a: `ExtractZip` hatte die Stream-Kopie behalten und im Kommentar den Grund umgedreht — unter Windows für keinen Test sichtbar; `updatehilfen` prüft jetzt den Zeitstempel aus der Zip, den nur `ExtractToFile` übernimmt.
Abnahme: Builds DesktopGL + WindowsDX 0 Fehler · `dotnet test` **412/412** · `tools/spielablauf updatehilfen updatedienst update menue` grün (neu: `updatehilfen` und `updatedienst`, `update` neu geschrieben; unter Windows ersetzt der Test eine wirklich laufende Exe — `ping.exe` als Stellvertreter); Mutantenlauf 15 von 15 gefunden · `checks/u2_einbindung.py`, neu `checks/start_menue.py gl|dx` (Startprobe ins Hauptmenü — `start_rts.py` zeichnet das Menü nie) · **echter End-to-End-Test gegen GitHub** mit der angenommenen Fassung: Kopie der offiziellen v0.4.4 (WindowsDX) läuft, das Update lädt das echte v0.4.5-Release (185 MB, Fortschritt live), ersetzt 770 Dateien unter der laufenden Exe, startet v0.4.5 (FileVersion 0.4.5.0) aus demselben Ordner, Aufräumen ohne Reste · Fotos des Menüs in vier Zuständen (aktuell, Angebot, Laden 37 %, Fehler). **Offen:** wirkt erst ab dem nächsten Release (v0.4.6) — Installationen bis v0.4.5 haben noch das alte Update und entpacken v0.4.6 ein letztes Mal verschachtelt (als DesktopGL); von dort aus läuft jedes weitere Update richtig. Wer die alte Verknüpfung behält, lädt v0.4.6 einmal von Hand. (Stand 2026-10-23: v0.4.6 bis v0.4.8 sind ausgeliefert, aktueller Stand 0.4.9 — der Hinweis betrifft nur noch Installationen ≤ v0.4.5.) In `release/publish/v0.4.4/win-x64-windowsdx/` liegt noch `AoE_Evolutions_v0.4.5/` vom alten Update — kann weg.

Davor: **Reiter-Geschwindigkeit: alle Reiter ~2,25× schneller — das 3,1-fache der Fußsoldaten** (2026-10-20) — Wünsche: „die reiter müssen sich etwa 50% schneller bewegen“, „oder etwa doppelt so schnell wie soldaten“, danach „ok er muss noch etwa schneller werden. nochmal etwa 50%“ (weitere Runde +50 % über alle Reiter).

`UnitTypes.cs`: Späher (Scout) 1,2 → 2,7, Ritter 1,0 → 2,25, Kamelreiter 1,0 → 2,25 (Belegungseinheiten/Sekunde; Fußsoldaten liegen bei 0,7–0,8 — die Reiter sind damit gut 3× so schnell wie Miliz/Speer). Test `Scout_HasBetterVision` entsprechend angepasst (1,2f → 2,7f).
Abnahme: `dotnet build AgeOfEvolutions.slnx` gesperrt durch das laufende Spielfenster (AoE.Core.dll wird von AgeOfEvolutions (31860) blockiert, MSB3027) → isolierter Build von `AgeOfEvolutions.Core`: **0 Fehler, 0 Warnungen** · `dotnet test` **412/412** · `tools/ai-pruefung` alle Prüfungen ok (beide KI-Instanzen, Angriff zeigt Wirkung) · `tools/spielablauf bewegen soldaten angreifen` auf 3 Karten grün. **Offen:** voller Solution-Build (Kopie in die DesktopGL/WindowsDX-Bins) erst, wenn das offene Spielfenster des Users geschlossen ist; neue Werte greifen beim nächsten Spielstart.

Davor: **Beerenbüsche: am Waldrand statt um die Waldmitte — die Karte trägt jetzt ~127 statt ~13 Büsche** (2026-10-19) — Wunsch: „Wie ist die verteilung der beerenbüsche. Hier scheint es etwas wenige zu geben“.

Ursache: `PlaceBerryBushes` streute je Wald 9–16 Büsche in der ±2-Kiste um die KlumpenMITTE. Der Wald trägt aber Radius 3–5 (Soft-Kreis von `InSoftCircle`), also lag fast jede Kachel der Kiste im eigenen Wald und wurde als „keine Wiese“ verworfen — Wälder mit Radius 5 trafen garantiert null Büsche, auf der Karte blieben im Schnitt 13,4 von den ~130 gesetzten. Zusätzlich überschrieben gesetzte Büsche still Schaf-/Reh-/Wildschwein-/Kaninchen-Kacheln (Food-Wechsel ohne Prüfung).
Neu: `AddResourcePatches` trägt mit jedem Wald seinen Radius weiter (`List<(int x, int y, int radius)>`); `PlaceBerryBushes` stellt die Büsche in einem Band AM WALDRAND (r+1 bis r+2, sicher außerhalb des Soft-Kreis — dort liegen auch die 70-%-Lichtungen) um jeden Wald, bis 12 Winkelversuche je Busch. Eine Kachel mit Gebäude oder bereits stehendem Wild (`Food != FoodSource.None`) wird nicht überschrieben. `kartenpruefung`: neue Gruppe `beeren` (min. 15 Büsche je Karte, sonst Exit 1).
Abnahme: `dotnet build AgeOfEvolutions.slnx` (0 Fehler, 0 Warnungen) · `dotnet test` **412/412** · `tools/kartenpruefung rohstoffe start wild beeren` grün — Beeren 127,1 je Karte (vorher 13,4), Rehe 71 % der Schafe (bleibt im 60–100-%-Band) · `tools/ai-pruefung` alle 9 Prüfungen ok (beide KI-Instanzen aktiv) · `tools/spielablauf werkzeug arbeitsklang` auf 3 Karten grün — die bisher optionalen Beeren-Abzweige (Sickle-Werkzeug, „rascheln“-Klang) laufen jetzt auf, da jede Karte Beeren trägt. **Vorbestehend, nicht von diesem Fix:** die `groessen`-Gruppe scheitert auf der 32×32-Testkarte an Stein-/Gold-Dichte (182 %/236 % > 160-%-Obergrenze) — git-stash-Nachweis: auch OHNE diesen Fix rot (Gold 231 %). Die festen Startvorräte (Stein + Gold je Stadtzentrum) zählen auf der kleinen Karte zu stark mit; Obergrenze der Prüfung oder die Startvorräte je Fläche gehören angepasst (eigener Task).

Davor: **Update-System: Version im Menü, Angebot auf neue GitHub-Releases + Auto-Download, Installation, Neustart** (2026-10-18) — Wünsche: „update system im spiel eingebaut und auch im menü sichtbar … Kann man von dort automatisch neuere Version laden …“, „es soll wirklich schon automatisch runterladen und installieren. Vorher noch prüfen auf was für einem Computer die software läuft“ (bei Mac die Mac-Version), „auch: eventuell werden dlls und exe während der ausführung geblockt“ und „nehme an das game muss dann neu gestartet werden“.

`UpdateService` (neu, `AgeOfEvolutions.Core/Update/`) prüft gegen die öffentliche GitHub-Release-API (`api.github.com/repos/gyurasan/AoE-Game/releases/latest`, 5-s-Timeout, User-Agent; `CheckLatest()` wirft nie, läuft in einem Task) und vergleicht das `tag_name` mit der AssemblyVersion. **Versionsnummer:** `Directory.Build.props` trägt `<Version>0.4.3</Version>` als Single Source of Truth (ohne Angabe fällt MSBuild auf 1.0.0.0 → Menü „Version 1.0.0“ — fixiert; `release/publish_app.py` + `make_release_zips.py` lesen die props-Version selbst, Publish ohne `-p:Version` verifiziert → 0.4.3.0). **Automatik-Pipeline** (`BeginUpdate`, nach Klick, Task): 1) `PlatformZip` erkennt den Computer (OS + Architektur): Windows→`win-x64-desktopgl`, macOS M→`osx-arm64`, macOS Intel→`osx-x64`, Linux→`linux-x64`, unbekannt → Browser-Fallback auf die Release-Seite; 2) das passende Zip wird heruntergeladen (Fortschritt in MB unter der Menü-Zeile, atomar als `.part` → verschieben, 15-min-Timeout); 3) SHA256-Prüfsumme aus dem Release-Asset `SHA256SUMS`, fehlt das Asset → Prüfung wird übersprungen; 4) Entpacken in ein **neues** Verzeichnis `AoE_Evolutions_v<tag>/` NEBEN der laufenden Installation — Windows sperrt laufende Exe/DLL gegen Überschreiben/Löschen, daher nie in den laufenden Ordner; 5) neue Version wird gestartet (`StartApp`). Phasen Idle→Downloading→Verifying→Extracting→Launching→Done/Failed, Fehler zeigen die Ursache („klicken = erneut“). **Neustart:** die neue Version öffnet automatisch; die ALTE Instanz schließt sich nach „installiert und gestartet“ per Klick auf die Menü-Zeile (`ExitApp`-Hook, Default `Environment.Exit(0)`). Testbarkeit ohne Netz/Browser/Datei: `FetchJson`, `OpenUrl`, `DownloadFile`, `FetchText`, `StartApp`, `ExitApp` sind austauschbare statische Hooks (alle im `update`-Spielablauf ersetzt, im finally wiederhergestellt). **Falle (.NET 10):** `ZipArchiveEntry.Name` liefert nur den Dateinamen OHNE Ordner — der volle Eintragspfad steht in `FullName`; `ExtractZip` nutzt `FullName`, sonst flacht der Top-Ordner des Release-Zips (`win-x64-desktopgl/`) im Ziel ab (2026-10-18 vom Test gefangen, vorher entpackte die Pipeline flach in die Install-Root).

Abnahme: Build AgeOfEvolutions.Core + DesktopGL + WindowsDX 0 Fehler, 0 Warnungen · `dotnet test` **412/412** · `tools/spielablauf update menue` grün — Gruppe `update`: v9.9.9 → Angebot, Klick → Download (Synthese-Zip mit `rid/`-Top-Ordner) → SHA256 korrekt → Entpacken → neue Exe `AoE_Evolutions_v9.9.9/<rid>/` existiert und wird gestartet; **die laufende, gepinnte Installation (`laufende-AgeOfEvolutions.exe` mit offenem Handle) bleibt bytegleich unverändert und die neue Version liegt nebenher**; Klick nach Done → `ExitApp`-Aufruf; falsche SHA256/fehlende Plattform-Zip → sauberer Abbruch ohne Entpacken; fehlende `SHA256SUMS` → Prüfung übersprungen, Update läuft weiter; Browser-Fallback öffnet die Zip-URL; aktuell/älteres Release/offline/kaputte Antwort → kein Angebot, kein Crash; Zeilen-Geometrie in 3 Fenstergrößen; `menue`-Gruppe bleibt grün · Live-API: `releases/latest` antwortet mit `tag_name v0.4.3`.

**Auswahl-Filter: gemischte Auswahl filtert pro Einheitenart** (2026-10-18) — Wunsch: „Was noch ein Problem ist. Wähle ich verschiedene Leute aus, z. B. Soldaten, Dorfbewohner und Bogenschützen, kann ich keine Gruppe selectieren. Hier müssen unten, wo die Buttons sind, Buttons angezeigt werden pro Typ — ein Button für Dorfbewohner, einer für Bogenschützen und einer für Soldaten, damit man in der Gruppe z. B. nur die Soldaten auswählen kann. Die Buttons sollen in einer NEUEN Button-Gruppe angezeigt werden, wenn mehr als nur eine Art ausgewählt ist“.

Wenn die Auswahl zwei oder mehr Einheitenarten mischt, legt `LayoutButtons` (RTSGameplayScreen) unter den Befehlstasten eine eigene Reihe an: eine Taste pro Art (Label=Name=GermanName — Dorfbewohner, Miliz, Bogenschütze, Späher haben ein Icon aus `NameIcons`, alles andere eine Text-Taste), Reihe auf `screenBounds.Height - 6 - size` (unterste Zeile, passt auch mit Builder-Zeilen). Der Klick ruft `SelectOnlyType(typ)`: die aktuelle Auswahl wird auf genau diese Art gefiltert (IsSelected für alle synchron, übrige Einheiten bleiben auf der Karte, Einheiten außerhalb der Auswahl — auch derselben Art — bleiben unberührt). Auswahl mit genau einer Art zeigt die Reihe NICHT (Regel `selectedTypes.Count >= 2`). Der Layout-Cache kennt die Art-Menge als neuen Zustand (`_buttonsLayoutSelectionTypes`, Distinct der Auswahl) — Auswahlwechsel löst das Re-Layout im nächsten Draw aus; bestellte Reihenfolge der Tasten = Enum-Reihenfolge der Art. Neue spielablauf-Gruppe `typauswahl`.
Abnahme: `dotnet test` **412/412** · `tools/spielablauf typauswahl` (neu; gemischte Auswahl 2 Dorfbewohner + Miliz + Bogenschütze → eine Taste je Art; Klick auf „Bogenschütze“ → Auswahl genau dieser Bogenschützen, IsSelected synchron, fremder Bogenschütze unberührt; danach keine Filter-Reihe mehr; zwei Dorfbewohner → keine Reihe) auf 3 Karten grün · `tools/spielablauf typauswahl leiste zeitalter neubauten auswahl linksklick ziele soldaten bewegen autoantwort kampfbild niederschlag pfeile verteidigung` auf 3 Karten grün (EXIT=0, keine Verletzungen).

**Kampf: Nahkampf erreicht höchstens 1 Kachel, Reiter stärker als Infanterie** (2026-10-18) — Wunsch: „kampf simulation ist noch nicht optimal. Die soldaten müssen näher ran als die Bogenschützen. Teilsweise konnte aber der soldaten den bogenschützen verletzen obwohl er mehr als 1 Kachel entfernt war. Zudem sind die berittenen Soldaten nicht stark genug. Normalerweise sollten die [Reiter] stärker als die Soldaten zu Fuß sein“.

Ursache 1, Reichweite: `InAttackRange` (RTSGameplayScreen) maß die Nahkampfreichweite Chebyshev (max. x/y-Abweichung) — ein Kachel-Diagonale ist 1,41 Kacheln und zählte trotzdem als „in Reichweite“. Nahkämpfer griffen damit sichtbar über eine Kachel an. Ursache 2, Balance: Ritter 10 Angriff / 100 HP, Miliz +12 und Speer +15 gegen Kavallerie — der Ritter schlug kaum härter als die Fußsoldaten und verlor das Duell mit dem Speerkämpfer.
Neu 1: `InAttackRange` misst Euklidisch (pixelgenau) mit 0,25-px-Gleitkomma-Spielraum (der Standpunkt liegt höchstens stop + spacing vom Ziel — er muss immer in Reichweite bleiben, sonst pendelt die Einheit): Nahkampf erreicht höchstens genau 1 Kachel (0,5 Anhalt + 0,5 Abstand), der Bogen einen 4,5-Kachel-Ring — Kreise statt Kachel-Quadrate. Die Stellschrauben `MELEE_STOP_TILES`/`COMBAT_SPACING_TILES` (je 0,5) bleiben unberührt. Neu 2 (`UnitTypes.cs`): Ritter 120 HP / 12 Angriff (vorher 100/10), +5 gegen Bogenschützen bleibt; Speer +15 → +10, Miliz +12 → +6 gegen Kavallerie — der Konter bleibt ein LEICHTER Konter (Speer trifft weiter härter als Miliz), dreht das Duell aber nicht. Alte Speer-Rüstung +1 gegen Kavallerie entfernt: dead code, der Schadens-Berechner liest Rüstung nur pro Verteidiger-Klasse. Resultat der 1-gegen-1 (gleiches Schlagtempo): Ritter schlägt Miliz in 4 Schlägen (dafür 9), Speerkämpfer in 4 (dafür 8), Bogenschützen in 2 (dafür 15 Pfeile, während der Ritter anläuft), Kamelreiter in 5 (dafür 7).
Abnahme: `dotnet build AgeOfEvolutions.slnx` (0 Fehler, keine neuen Warnungen — die 15 vorbestehenden in EconomyAi/EconomyAiTests/SaveGame/GameLog) · `dotnet test` **412/412** (neu `CounterTriangleTests.Ritter_IstStaerkerAlsInfanterie`: weniger-mehr-Schläge-Regel statt fester Zahlen + Konter-Richtung bleibt wahr; `Spearman_HasCavalryBonus` 13, `Knight_HasArcherBonus` 17) · `tools/ai-pruefung` alle 9 Prüfungen ok (beide KI-Instanzen aktiv, Angriff zeigt Wirkung) · `tools/spielablauf angriff ziele soldaten verteidigung kampfbild blick schlag zielwechsel klangbrett kampfklang niederlage pfeile bewegen` auf 3 Karten grün.

**Auto-Antwort: nahe Soldaten greifen angreifende Feinde von selbst an** (2026-10-18) — Wunsch: „Und ich würde auch die logik etwas optimieren. Wenn die Gegner sichtbar sind und sie angreifen, sollten meine Soldaten in der Nähe automatisch angreifen, nicht nur die, die angegriffen werden“.

`AutoRespond` (RTSGameplayScreen, am Anfang von `UpdateUnits` je 0,5 s, gleicher Takt wie K6): sichtbarer Feind greift eine eigene Einheit ODER ein eigenes Gebäude an (State Attacking — NICHT während des Anmarschs) → der eigene untätige Soldat IN DER NÄHE (max. 6 Kacheln, `AUTO_RESPONSE_RADIUS_TILES`), ohne Einheit-/Gebäude-/Sammel-/Bau-Auftrag und keine Dorfbewohner (K6-Regel; Spieler-Befehl Vorrang, Ping-Pong-Schutz), greift den nächsten sichtbaren Angreifer über `AttackUnit` an. Beide Spieler symmetrisch. K6 (15-Kachel-TC-Ring) bleibt als zusätzlicher Pfad; K6-Verteidigung und Auto-Antwort nutzen denselben sauberen Angriffskanal.
Abnahme: `dotnet test` **412/412** · `tools/spielablauf autoantwort` (neu; naher Soldat 3 Kacheln: antwortet erst nach dem ersten Schlag des Feindes und trifft den Angreifer; Soldat 12 Kacheln fern: bleibt ohne Ziel; Szenario in der Kartenmitte, damit K6 nicht mischt) · `tools/spielablauf autoantwort verteidigung zielwechsel soldaten angriff blick schlag kampfklang niederlage pfeile bewegen` auf 3 Karten grün · `tools/ai-pruefung` alle 9 Prüfungen ok (Kämpfe aggressiver: jetzt fällt eine Kaserne statt des Schießstands — beabsichtigt) · isolierte Builds (AoE.Core, AgeOfEvolutions.Core → isoliertes Verzeichnis, spielablauf): 0 Fehler, keine neuen Warnungen. **Offen:** voller DesktopGL/WindowsDX-Build + Startprobe erst, wenn das laufende Spielfenster des Users (lockt bin/Debug: MSB3027) geschlossen ist.

Davor: **Animationen: Bauphasen, Einsturz, Pfeile (A1–A3)** (2026-10-17) — Wunsch: „ich würde noch gerne mehr Animationen im Game haben. Zum Beispiel wenn ein Gebäude zerstört wird soll das animiert werden. Wenn ein Geäude erstellt wird sollten ebenfalls 3-4 Darstellung sich im Bau befindliches Gebäude Dargestellt werden. Zudem soll man die Pfeile der Bogenschützen sehen. Sollte also auch animiert sein“.

Neue Teildatei `Screens/RTSGameplayScreen.Animationen.cs` (die Klasse ist dafür `partial`). **A1 Bauphasen:** `ConstructionStage` teilt den Fortschritt in vier Viertel. `DrawConstructionStages` zeigt zuerst den abgesteckten Platz mit Pflöcken, Schnur, Stämmen und Steinen. Danach wächst das Bild des fertigen Gebäudes (Zeitalter und Farbe des Besitzers) von unten im Gerüst mit Stangen, Brettern und Schrägstreben: Fundament 30 % in rohem Ton, Rohbau 65 %, fast fertig 100 % mit Seitengerüst. Ohne Gebäudebild bleibt der gezeichnete Bauplatz. **A2 Einsturz:** `DestroyBuilding` entfernt das Gebäude aus der Logik wie bisher sofort, `StartCollapse` merkt es sich für die Animation. In 1,4 s wackelt und versinkt der Bau, 3,2 s lang quillt Staub (`PuffTexture`). Danach liegen Mauerreste, Asche, Steine und verkohlte Balken, die nach 45 s in 8 s verblassen; sie stehen in der Tiefenschicht (Kind 7). **A3 Pfeile:** `MaybeLaunchArrow` lässt den Pfeil eines Fernkämpfers so los, dass er genau beim Treffer landet (Flugzeit aus der Strecke, 0,15–0,6 s). Treffer, Schaden und Bogengeräusch bleiben, wo sie waren. Die Flugbahn ist eine Wurfparabel (`ArrowAt`, Scheitel 22 % der Strecke). Gezeichnet werden Schaft, Metallspitze, rote Federn und ein Schatten am Boden; Ziel ist die Brust einer Figur oder die Mitte eines Gebäudes. `UpdateEffects` lässt Pfeile und Einstürze in `UpdateUnits` altern; im Nebel bleibt beides unsichtbar.
Abnahme: Build DesktopGL + WindowsDX · `dotnet test` **411/411** · neue Gruppen `tools/spielablauf bauphasen einsturz pfeile` (Phasengrenzen; Einsturz an der Stelle des Gebäudes, altert mit der Spielschleife, Trümmer 45 s; im Zweikampf landet jeder Pfeil im Bild des Treffers, Miliz ohne Pfeile, Pfeile auf ein Haus in dessen Mitte) auf 30 Karten grün · alle 50 Gruppen grün · Mutantenlauf 8/8 · Startprobe GL + DX · Foto der Fassung des Modells gegen das der Referenz: 0,95 % abweichende Pixel bei 0,99 % Rauschen zwischen zwei Referenzläufen. Prototyp und Fotos im Scratchpad (Foto-Patch mit `AOE_FOTO_ANIM`), Umsetzung vom lokalen Modell (A1 42 s; A2 im zweiten Lauf nach Rückmeldung zu `((h >> 3) % 3) switch` und je einem Schleifendurchlauf zu viel; A3 28 s). Offen: Sichtprüfung im Spiel, Geräusch der Sehne erst beim Treffer statt beim Abschuss.

**Fester Boden: Gras ändert sich nie, Pfade bleiben (G15)** (2026-10-17) — Wunsch: „gras und pfäde dürfen sich nicht mehr ändern wenn sie mal erstelt sind. Über gras kann nur noch der Pfad oder Strassen gelegt werden. Aber Gras in ein anderes Gras nicht“ (nach G14, wo das nur im Nebel galt).

Ursachen: `TallGrassAt` meldete auf jeder Kachel mit Tier 0. Wandernde Schafe, Rehe, Kaninchen und Wildschweine schoben so Löcher durchs hohe Gras, und jeder Tritt drückte das Gras um 10 % herunter. Das Lebendbild zeigte schon den ersten Tritt als niedergetretenes Gras, und `RegrowGrass` ließ jeden Pfad in 15 Minuten wieder zuwachsen. Gefällter Wald verlor den Waldboden (Kleegras, Schatten), und wo Wald, Stein oder Gold abgebaut war, wuchs plötzlich hohes Gras.
Neu: `Tile.GroundType` hält den Boden der Kartenerzeugung fest (`TileMap.RememberGround`, im Spielstand als `TileSave.GroundType`; ältere Spielstände nehmen den Typ). `Tile.IsPath` gilt ab `ROAD_WEAR_MIN` (sechs Tritte). Ein Pfad wächst nie wieder zu; Tritte darunter sieht man nicht, und sie vergehen wie bisher (`RegrowGrass`), damit nur oft begangene Strecken Pfade werden. `TallGrassAt` hängt nur noch an Karte und `GroundType` und ist 0 nur unter Pfad, Weg, Gebäude, Feld und Beerenbusch. Das Lebendbild zeigt rot einen Pfad immer gleich (1, unter einem Weg 0,35) und grün den Waldboden auch nach dem Fällen.
Abnahme: Build DesktopGL + WindowsDX · `dotnet test` **411/411** (neu `FesterBodenTests`, 9 Tests) · neue Gruppe `tools/spielablauf festerboden`: zwei Minuten Spielzeit mit wandernden Tieren, kein Gras ändert sich; fünf Tritte unsichtbar, der sechste ein Pfad, der bleibt; Schaf, Fällen und Abbau ändern nichts; Weg und Beerenbusch verdecken das Gras. Auf 30 Karten grün, dazu `pfad` (angepasst: Pfade bleiben) und `nebelboden strassen gebaeudewege bauen` · Mutantenlauf 12/12 gefunden · Startprobe GL. Umsetzung vom lokalen Modell (Agent G15a 39 s, G15b 137 s), beide im ersten Lauf angenommen, gleich der Referenz. Offen: Sichtprüfung im Spiel.

**Wege von allen Gebäuden zum Dorfzentrum (C16c)** (2026-10-17) — Wunsch: „Weiter wäre gut die Logik anzupassen wie die Strassen erstellt werden. Hier wäre Strassen von allen Geäuden zum Dorfzentrum gut“.

Bisher suchte `TileMap.ConnectBuilding` per Breitensuche den nächsten beliebigen Weg, höchstens 30 Kacheln weit. Ein Gebäude dockte so auch an ein loses Stück Trampelpfad an, das nirgends hinführte. Weiter draußen bekam es gar keinen Weg, und das Stadtzentrum legte sich selbst einen Weg zum nächsten Pfad. In einer Simulation auf generierten Karten blieben 2 von 13 Gebäuden ohne Verbindung zum Stadtzentrum. Neu: Der Weg führt immer bis an den Ring eines eigenen Stadtzentrums (des nächsten, falls es mehrere gibt), ohne Höchstlänge (`ROAD_CONNECT_MAX_TILES` entfällt). Gewählt wird der nach Kosten kürzeste Weg (Dijkstra): Eine vorhandene Wegkachel kostet 1, eine neue `ROAD_NEW_TILE_COST` = 3. So werden vorhandene Wege und Trampelpfade mitbenutzt, wo der Umweg kurz ist, und die Wege bündeln sich zu Straßen. Unter gleich teuren Wegen gewinnt der, dessen Kacheln dem Stadtzentrum am nächsten liegen. Neue Wege laufen deshalb als Treppe (im Wege-Shader schräg) auf das Zentrum zu statt im rechten Winkel. Ein Stadtzentrum bekommt selbst keinen Weg. Der Bildschirm ruft `ConnectToRoads`/`ConnectAllToRoads` unverändert auf, nur die Kommentare sind angepasst.
Abnahme: Build DesktopGL + WindowsDX · `dotnet test` **402/402** (`FeldwegeTests`: 17 statt 12 Tests zu Gebäudewegen, u. a. schräger Verlauf, loser Weg ist kein Ende, langer Umweg wird nicht genommen, weit draußen, nächstes von zwei Zentren) · Mutantenlauf 10/10 gefunden · `tools/spielablauf gebaeudewege` (neu: genau der kürzeste Weg nach dem Aufstieg, Haus weit draußen) auf 30 Karten grün, dazu `strassen bauen neubauten speichern` · 60 Anschlüsse auf der 128er-Karte ≈ 0,1 s. Umsetzung vom lokalen Modell (Agent W4, 326 s). Der erste Lauf scheiterte am Build (`OrderBy(Near)` mit einem Tupel, CS0411); die zwei Aufrufe habe ich von Hand korrigiert, weil ComfyUI die Grafikkarte brauchte. Danach legt die Fassung des Modells dieselben Wege wie die Referenz. Offen: Sichtprüfung im Spiel.

**Fehler-Log (Logger)** (2026-10-17) — Wunsch: „einen logger einbauen, damit Fehler in eine Datei geschrieben werden“, Log im Ordner `logs`, dieser in `.gitignore`.

Neu `AgeOfEvolutions.Core/Diagnostics/GameLog.cs`: `GameLog.Info/Warn/Error` (+ `Error(Exception, Kontext)` mit Stacktrace), thread-sicher, schreibt je Kalendertag in `logs/spiel-jjjj-mm-tt.log` neben der Exe; das Loggen selbst kann nie crashen (Schreibfehler werden geschluckt), Fehler zusätzlich auf `Console.Error`. Fangnetze in `AgeOfEvolutionsGame` (Konstruktor): `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` (mit `SetObserved`), `Exiting` → „Spiel beendet“, dazu Start- und Endzeile. Beide Programme (DesktopGL, WindowsDX): `game.Run()` in try/catch → `GameLog.Error` + ExitCode 1. Angezapft: `LoadOptional` (fehlendes Bild → Warn mit Asset-Namen, statt unsichtbar zu verschwinden), `--laden`-Fehler, AI-API-Start. `.gitignore` trägt jetzt `logs/`.
Abnahme: `dotnet build` (0 Fehler, keine neuen Warnungen) · `dotnet test` **397/397** (neu `GameLogTests`, 3 Tests) · `tools/spielablauf bewegen` auf 3 Karten grün · E2E: Testinstanz mit `--laden <fehlender Spielstand>` → `ERROR`-Zeile mit Stacktrace + `WARN: Asset fehlt`-Zeilen in `logs/spiel-…log` neben der Exe → Instanz beendet. **Beifang:** Das Log zeigt 70 fehlende Assets (Gebäude/Soldaten je Zeitalter: `Gebaeude/{dunkel,feudal,ritter,imperial}/*`, `Einheiten/dunkel/dorfbewohner_*`, `_schlag1`-Rahmen, `Gebaeude/{kaserne,markt,palisade,…}_blau|rot`) – nirgends auf Disk (noch nicht via uebernehmen.ps1 übernommen), dafür aber im Code gesucht — Kandidaten für die Bild-Produktion. Windows-Pitfall gelöst: offenes .NET-Handle blockierte hier paralleles .NET-Lesen (bash-Reader ging) – GameLog gibt daher pro Zeile öffnen-schreiben-schließen statt dauerhaft offenem StreamWriter.

**Wege sehen wie Wege aus (C16b)** (2026-10-16) — Feedback: „in der Feudalzeit sehen die "Feldstrassen" nicht wirklich wie strassen aus. Hier muss noch sehr nachbessert werden und auch die anderen epochen prüfen bitte“ und „es muss schon zum rest des games passen und nach "strasse" oder Pfad aussehen“.

Ursache: Der Bodenshader malte die Wegkacheln weich als Fläche, und jede einmal betretene Kachel wurde beim Aufstieg Weg - um das Dorf entstanden Pflaster-, Erd- und Schotterflecken statt Wege. Neu:
- Eigener Durchgang `Effects/Wege.fx` über dem Boden: Linien fester Breite zwischen den Kantenmitten benachbarter Wegkacheln (geradeaus durch, Kurven schräg abgeschnitten, Sackgassen bis zur Mitte, volle 2×2-Blöcke gefüllt) - Treppen aus Kacheln werden Diagonalen, Ecken Kurven, nach oben führt der Weg an die Tür. Ausgewertet werden je Bildpunkt nur die vier Kacheln um die nächste Ecke (passt in ps_3_0).
- Je Zeitalter ein Aussehen (`DrawRoads`): Feudalzeit Pflasterweg mit Randsteinen (Halbbreite 0,24), Ritterzeit ausgefranster Erdweg mit zwei Fahrspuren und Grasnarbe (0,27), Imperialzeit gewölbter Schotterweg (0,30); Farbtöne gedämpft, damit sie zur gemalten Wiese passen; daneben ein halbdurchsichtiger Saum.
- Wegkarte mit Nebel-Gedächtnis (`_roadMapData` in `UpdateGroundMemory`, `RoadDrawnAt`: Wiese/Sand ohne Gebäude, Feld und festen Rohstoff - ein Schaf unterbricht den Weg nicht); `RoadDrawData` füllt schräge Lücken auf, damit der Shader nur gerade Nachbarn braucht; unter Wegen zeigt der Bodenshader nur niedergetretenes Gras (rot ≤ 0,35).
- Weg erst ab sechs Tritten (`TileMap.ROAD_WEAR_MIN` 0,3): beim Aufstieg und für neue Tritte ab der Feudalzeit. Weil das Gras zwischen den Gängen nachwächst, braucht ein Pfad im Test rund acht Gänge.
- Die eigentliche Ursache des schwarzen GL-Bilds (W1d) gefunden: dasselbe Bild an zwei Texture Units, auch über Durchgänge hinweg (das Rauschbild nutzen Boden- und Wege-Shader). `ReleaseTextureUnits` gibt nach jedem eigenen Shader-Durchgang alle Units frei.
Abnahme: `dotnet build` GL und DX (0 Fehler) · `dotnet test` **394/394** (Feldwege-Tests auf sechs Tritte umgestellt, neu `EinzelneSchritteMachenKeinenWeg`) · `tools/spielablauf`: 24 Gruppen auf je 3 Karten grün, neu `wegkarte`, `strassen` geht jetzt bis zur Schwelle, `nebelboden` prüft auch die Wegkarte · `checks/c16b_wege.py karte|daten|zeichnen|shader` · Startprobe GL/DX · Fotos aller vier Zeitalter in GL mit Nebel; das Ergebnis des Modells weicht vom Prototyp im Mittel um 0,005 Farbstufen ab. Umgesetzt vom lokalen Modell nach Verträgen aus einem am Foto abgestimmten Prototyp: W3a, W3b, W3c im ersten Lauf, W3d im zweiten (`n00`/`n10` als float statt float2, X3018).

**Boden im Nebel (G14)** (2026-10-16) — Feedback: „Sichtbares gelende verändert den gras typ obwohl man niemanden dort sieht. Das sollte nicht sein. Auch wenn tiere durchlaufen die man nicht sieht sollte sich das Gras nicht ändern.“

Ursache: `RefreshGroundLive` übertrug alle 0,25 s jede Kachel ins Lebendbild des Bodenshaders, auch im Nebel - Trampelpfade und Wege unsichtbarer Gegner erschienen. Und `TallGrassAt` meldet auf Kacheln mit Tier 0: Die Büschel des hohen Grases wichen jedem unsichtbaren Schaf und Reh aus, ein wanderndes Loch im Gras.
Jetzt merkt sich der Boden, was Spieler 0 zuletzt gesehen hat: `UpdateGroundMemory(all)` zieht nur sichtbare Kacheln nach (Spielbeginn und Laden: alle), `RefreshGroundLive(all)` lädt hoch, die Büschel lesen `SeenTallGrassAt`. Sieht wieder jemand hin, gilt nach spätestens 0,25 s der Ist-Zustand.
Abnahme: Gruppe `nebelboden` in `tools/spielablauf` (3 Karten: Pfad, Tier und Weg im Nebel unsichtbar, Pfad in Sicht sofort, beim Hinsehen nachgezogen, Komplettabgleich nimmt den Nebel mit) · `checks/g14_nebelboden.py` · Startprobe GL und DX. Umgesetzt vom lokalen Modell (145 s). Der Lauf wurde erst abgelehnt, aber nicht wegen G14: `gebaeudewege` hatte auf einer Zufallskarte einen Bauplatz ohne möglichen Weg gewählt. Die Prüfung nimmt jetzt nur anschließbare Plätze (36 Karten in Folge grün), danach lief die Fassung des Modells durch die ganze verify-Liste.

**Wege von Gebäuden und Wegbilder je Zeitalter (C16)** (2026-10-16) — Feedback: „Hier geht es um in der Dunkelzeit wären es wohl Trampelpfad danach Pflasterstraße (Mehrschichtig: Stein, Zement), danach Mittelalter (Erde, Schlamm, Kies) am ende Verdichteter Schotter … Zudem sollten dann auch automatisch von den erstellten Häusern pfade und Strassen erstellt werden (allerdings noch nicht in der ersten epoche).“ und „benutze bitte auch die lokale AI dafür“.

Wegbilder (Qwen-Image, Gruppe `wege` in `tools/bilder/bilder.json`): Feudalzeit Pflasterstraße aus kleinen Steinen in hellem Zement (Seed 5801, auf 664 px verkleinert, Naht per Image Quilting), Ritterzeit Erde mit Schlamm und Kies (5781), Imperialzeit verdichteter Schotter (5761); in der Dunklen Zeit bleibt der Trampelpfad. Die Reihenfolge ist die des Nutzers (historisch Römerstraße → Erdweg des Mittelalters → Makadam), auch wenn der Weg der Ritterzeit schlichter aussieht als das Pflaster davor - ein Tausch wäre nur die Zuordnung der drei `ziel`-Einträge. Gezeichnet im Bodenshader statt als Kachelquadrate: Wegkacheln stehen im Lebendbild rot = 1, `RoadSurface`/`SoilCover` legen das Bild der erreichten Stufe (`RoadTexture`, `RoadOn`, `RoadScale`, `ROAD_TEXELS` 4) mit schärferem Rand als einen Trampelpfad hin, helle Bilder gedämpft.
Wege von Gebäuden: `TileMap.CanCarryRoad`/`ConnectBuilding` - Breitensuche von der Vorderseite (Reihe unter dem Gebäude, sonst der Ring) zum nächsten Weg oder an ein eigenes Stadtzentrum, höchstens 30 Kacheln, um Wasser, Wald, Rohstoffe, Felder und Gebäude herum. Der Bildschirm schließt ab der Feudalzeit des Besitzers jedes fertige Gebäude an (`ConnectToRoads`, Mauern nicht) und beim Aufstieg alle vorhandenen, die dem Stadtzentrum nächsten zuerst (`ConnectAllToRoads`). Nebenbei behoben (Lücke aus C12): nach dem Laden stand `CurrentRoadLevel` auf 0, neue Tritte wurden keine Wege - `SaveGameMapper.Restore` setzt es jetzt aus den Zeitaltern.
Werkzeug: `tools/bilder/naht.py` und `"kachelbar": "naht"` in `uebernehmen.ps1` - Fugenmuster nahtlos ohne die Geistersteine des Überblendens.
Abnahme: `dotnet build` GL und DX (0 Fehler) · `dotnet test` **393/393** (13 neue `FeldwegeTests`) · `tools/spielablauf`: 22 Gruppen auf je 3 Karten grün, darunter neu `gebaeudewege` (Dunkle Zeit kein Weg, Aufstieg verbindet, neues Haus bekommt seinen Weg, Mauer nicht, Gegner erst nach dem eigenen Aufstieg) · `checks/c16_wegbild.py shader|screen|bilder` · Startprobe GL/DX · Fotos aller vier Zeitalter (Scratchpad-Kopie mit Foto-Schaltern, nicht im Arbeitsbaum). Umgesetzt vom lokalen Modell nach Verträgen: W2a, W2a2 (die Rückverfolgung brach an der Kartenecke (0, 0) ab - Test `WegDurchDieKartenecke` nachgelegt), W2b, W1a, W1b, W1c, alle im ersten Lauf angenommen (18-128 s).
Nachtrag W1d (Nutzer: „die GL Variante zeigt kein Bild mehr an wenn das spiel gestartet wird ist die ganze spielfläche schwarz. DX scheint noch zu gehen“): `DrawGroundShaded` gab in der Dunklen Zeit das Sandbild als `RoadTexture` - unter OpenGL hing damit dieselbe Textur an zwei Samplern des Bodenshaders, und der Nebel-Durchgang danach deckte die ganze Spielfläche schwarz. Jetzt immer ein echtes Wegbild (`RoadTextureFor(Math.Max(1, …))`), sichtbar macht es allein `RoadOn`. Durchgerutscht, weil die Startprobe nur die Laufzeit prüft und alle Fotos mit abgeschaltetem Nebel entstanden; eingegrenzt per GL-Fotos (Nebel aus: Bild da; Shader aus HEAD: Bild da; Sandbild durch Wegbild ersetzt: Bild da). `c16_wegbild.py screen` verbietet das Sandbild als Ersatz.

Davor: **Reparatur: beschädigte Gebäude wiederherstellen (C15)** (2026-10-16) — Feedback: „Angegriffene Gebäude sollten repariert werden können. Wenn sie zBs zu 50% angeschlagen sind müssen auch nur 50% der Rohstoffe verwendet werden um sie wieder herzustellen.“

Regel: Kosten und Dauer fallen anteilig zum fehlenden Schadensvolumen (Baupreis × fehlender Anteil, je Rohstoff mindestens 1; unverletzt = 0 Kosten). Dorfbewohner reparieren wie sie bauen: anklicken (eigenes beschädigtes Gebäude mit ausgewählten Dorfbewohnern) schickt sie an den Rand; der Auftrag kostet VOR dem Start einmalig, dazukommende Arbeiter nichts Mehr. Ohne Arbeiter steht er still. Geschwindigkeit wie am Bau: (n+2)/3. Fertig ist bei voller Standfestigkeit, Arbeiter gehen zur nächsten Baustelle oder werden untätig. Sicht & Meldung: Lebensbalken zeigt den Zustand, die Leiste „Reparatur p %“, Meldungen für Auftrag/Kosten/Fehlbeträge.

Umsetzung: `AoE.Core.Economy.Repair` (Auftrag: Sekunden, fehlende LP, Fortschritt; `ToState`/`FromState`), `BuildingRules.MissingFraction/RepairCostOf/RepairSecondsOf`, `Building.Repair` (TileMap-Data), `BuildingSave.Repair` + `SaveGameMapper` beide Wege. Screen: `RepairBuildingFor` (Order + Kosten + Zusenden), `UpdateRepair` (Tick, neben UpdateConstruction), `ReleaseRepairers`; Zustandsmaschine: Ankunfts- und Building-Zweig auch für `IsComplete`-Gebäude mit laufender Reparatur; HUD-Statuszeile.
Abnahme: `dotnet build AgeOfEvolutions.slnx` (0 Fehler, keine neuen Warnungen) · `dotnet test` **380/380** (11 neue `ReparaturTests`) · `tools/spielablauf -- reparatur` — 3 Zufallskarten grün: 50 % Schaden → 13 Holz (Hälfte von 25) und 12,5 s mit 1 Reparateur (halbe Bauzeit), 9,6 s mit 2 (AoE-II-Formel), unverletztes Gebäude weder repariert noch bezahlt.

**Straßen neu gedacht: Wege wachsen mit der Zeit (C12)** (2026-10-16) — erst war die „Straße“ kachelweise gebaut (Taste D, 10 Holz, A*-Kosten-Bevorzugung); der Nutzer hat das verworfen: „ok, das war nicht die idee mit den Strassen. Die Trampelpfade zu Gebäuden aus der Dunklen Zeit werden automatisch zu Feldwegen wenn man in die nächste Zeit wechselt. Mit jedem Aufstieg in eine neue Zeit werden dann die Wege besser und besser. Laufen die leute auf diesen wegen sind sie auch 20% scheller als auf der Wiese.“

Alles Manuelle ist rückbaukig entfernt (`BuildingType.Road`, Taste D, `Unit.RoadCell`, `TileMap.StartRoad/TickRoad/ActiveRoads/IsActiveRoad`, `MovementCost` in `Pathfinding`/`VisibilitySystem`, Straßen-Zweige im Screen und in `neubauten`). Neu: `Tile.RoadLevel` 0–3 ersetzt `IsRoad`/`RoadProgress` (auch im Spielstand: `TileSave.RoadLevel`, Mapper beide Wege). `TileMap.LevelOfAge` mappt Dunkel 0, Feudal 1, Ritter 2, Imperial 3; `TileMap.UpgradeRoads(level)` hebt alle vorhandenen Tritte (`Wear > 0`) und bisherigen Stufen an, es fällt nichts ab; `Trample` gibt neue Tritte ab der Feudalzeit direkt auf der erreichten Stufe, dunkle Zeit bleibt Stufe 0. Der Bildschirm ruft `UpgradeRoads` in `UpdateAges` bei jedem Aufstieg auf (beide Spieler). Laufbonus: `StepAlongPath` gibt 20 % auf, wenn die Kachel unter den Füßen `RoadLevel >= 1` — gemessen, nicht nur behauptet. Sicht: drei prozedurale 32×32-Wege (`Feldweg` mit Kiesel, `befestigt` mit festem Rand, `gepflastert` als Koppelpflaster auf Mörtel), optional `Boden/wege_feld`, `Boden/wege_befestigt`, `Boden/wege_pflaster`; Bodendurchlauf wählt nach `RoadLevel`, hohes Gras wächst auf Wegen nicht mehr.
Abnahme: `dotnet build AgeOfEvolutions.slnx` (0 Fehler; Warnungen nur die vorbestehenden in EconomyAi-/Test-Dateien) · `dotnet test` **369/369** (6 neue `FeldwegeTests`, 5 alte `RoadsTests` entfernt) · `tools/spielablauf -- strassen` — 3 Zufallskarten grün: Dunkle Zeit 10,1/10,0 s gleich schnell, Feudalzeit-Pfad 8,4 s (~20 % schneller: 10,1/1,2), neue Tritte ab Feudal sofort Stufe 1, Stufen 1 → 2 → 3 · `neubauten` grün (D→Straße wieder raus, Stadtzentrum-Sprung bleibt).

Davor: **Forschungswirkungen beweisbar (F2)** (2026-10-16) — Feedback: „sicherstellen das zBs das erstellen des Webstuhles auch auswirkungen hat. Oder wenn man das schmieden weiterentwicklt muss sich das ja auch irgendwie merkabar auswirken“.
Audit der Wirkkette (8/8 Forschungen sind verdrahtet, keine Logik-Lücke): Webstuhl, Schmiedekunst, Befiederte Pfeile, Schuppenpanzer und Maurerkunst ändern `Stats.BaseAttack/HitPoints/Range/BaseArmor` — genau die Werte, die `DamageCalculator` (Kampf Einheit) und `BuildingCombat` (Kampf Gebäude) live lesen; `Unit.AttackPower/Armor/AttackRange` sind Live-Properties auf `Core.Stats`, nichts wird gecacht. Die fertige Forschung wirkt einmal auf alles Bestehende (`UpdateResearch`: `TechEffects.Apply` je Einheit/Gebäude desselben Besitzers, der Gegner ist nicht betroffen) und auf alles Neue über `ApplyAll` (Ausbildung, neue Baustelle, Spielstand laden). Doppelaxt und Goldbergbau laufen als `TechEffects.GatherFactor` (×1,2 / ×1,15) in `GatherJob.Update`, das Pferdekummet als `TechEffects.FarmFood` bei `PlantCrop`.
Zwei echte Abnahme-Lücken gefüllt (Gruppe `forschung` in `tools/spielablauf`):
- **Goldbergbau war nie gemessen** — jetzt wie die Doppelaxt: ein Goldgräber schürft 0,39·1,15 ≈ 0,448 Gold/s; ohne die Forschung wären es 0,390, außerhalb der Toleranz → messbar wirksam.
- **Wirkungen im Kampf mit echten Schadenswerten:** Miliz → fremde Miliz +1 Schaden nach Schmiedekunst, fremde Miliz → eigener Dorfbewohner −1 nach Webstuhl, Miliz → eigenes Haus −1 nach Maurerkunst; jeweils vor und nach der fertigen Forschung gemessen, genau ±1 erwartet (`DamageAgainst`, `BuildingCombat.DamagePerHit`).
Abnahme: `dotnet build AgeOfEvolutions.slnx` (0 Fehler, 0 Warnung(en) zu neuen Stellen) · `dotnet test` **363/363** · `tools/spielablauf -- forschung` — 3 Karten grün: „Webstuhl 25 s, gleichzeitig bis 50 s, Holz 0.468/s, **Gold 0.437/s**, Maurerkunst 50 s; Wirkung nur beim eigenen Spieler, Abbruch erstattet“ (Holz 0,39·1,2, Gold 0,39·1,15).

Davor: **Spielstand speichern und laden (SV1–SV4)** (2026-10-14) — Feedback: „danach würde ich noch gerne das speicher des spieles implementieren“ und „neben Resume und quit was in deutsch angezeigt werden sollte wenn man esc drückt, sollte da auch speichern zu auswahl stehen. Im Hauptmenü braucht es dann noch das Spiel laden“.
Bedienung: Esc-Menü jetzt deutsch — **Fortsetzen / Spiel speichern / Beenden** (Beenden-Abfrage ebenfalls deutsch, mit Hinweis auf ungespeicherten Fortschritt); Speichern zeigt 1,5 s „Spiel gespeichert“ (oder „Speichern fehlgeschlagen: …“), das Menü bleibt offen. Hauptmenü: **„Spiel laden“** ist frei, sobald `%APPDATA%\AgeOfEvolutions\spielstand.json` existiert (ein Platz, neben `settings.json`; ein neuer Stand ersetzt den alten), sonst grau; ein unlesbarer Stand sperrt den Eintrag und meldet es. Startschalter `--laden [pfad]` beginnt direkt mit einem Spielstand (wie `--rts`).
Inhalt (JSON, Aufzählungen als Namen, Standardwerte weggelassen — Standardkarte ~200 KB): jede Kachel (Gelände, Rohstoff, Tier samt Wandertakt, Trampelpfad, Feld samt Nachwachsen), beide Spieler (Rohstoffe, Zeitalter samt laufendem Aufstieg, Forschungen, Erkundung als Maske), Gebäude (Baustelle, Lebenspunkte, Ausbildungsschlange samt Fortschritt und Kosten, laufende Forschung), Einheiten (Lage, Weg, Tempo, Zustand, Lebenspunkte, Sammelauftrag samt Traglast und Bruchteil, Baustelle/Angriffsziel als Index), Spielzeit, Animationstakt, Kamera (Weltpunkt in der Mitte + Zoom relativ zum Fenster — stimmt auch bei anderer Fenstergröße), KI-Stufe. Forschungswirkungen (Webstuhl, Maurerkunst …) werden beim Laden neu angewendet, danach die gespeicherten Lebenspunkte gesetzt.
Bewusst nicht gespeichert: Auswahl, Laufanimation (`_unitMotion`), Angriffswarnung, KI-Gedächtnis (`EconomyAi` startet frisch und plant aus der Welt), alle übrigen Spiel-Optionen (Tempos, Abstände, Lautstärke — gelten aus den aktuellen Einstellungen; nur die KI-Stufe kommt aus dem Stand). Die Tierlisten werden aus den Kacheln neu aufgebaut: der Kartengenerator trägt manche Tiere doppelt ein, nach dem Laden zählt jedes genau einmal.
Aufbau: Kernklassen mit `ToState`/`FromState`/`Restore` (`GatherJob`, `TrainingQueue`, `ResearchSlot`, `TechProgress`, `AgeProgress`, `Construction`); `Data/SaveGame.cs` (Datenklassen), `Data/SaveGameMapper.cs` (Welt ↔ Stand), `Data/SaveGameStore.cs` (Datei, erst `.tmp`, dann ersetzen); `TileMap.CreateBlank/RebuildWildAnimals/ExploredMask/RestoreExplored`; `RTSGameplayScreen(SaveGame)` + `CaptureSave/RestoreWorld/RestoreCamera`. Umgesetzt vom lokalen Modell nach Verträgen (SV1, SV2a, SV2b, SV3, SV4); abgelehnt und nachgebessert: SV1 (`HashSet.AddRange`), SV2 (verlaufen → in 2a/2b geteilt), SV3 (`switch` auf die KI-Profile, die keine Konstanten sind; einmal Spiegel ohne `Program.cs`).
Abnahme: Build DesktopGL und WindowsDX (0 Fehler) · `dotnet test` **363/363** (+53: `SaveStateTests` je Kernklasse mit Negativfällen, `SaveGameTests` mit Ringschluss speichern → JSON → laden → speichern zeichengleich, 25 Läufe auf Zufallskarten grün) · `tools/spielablauf -- menue speichern bauen weiterbauen farm angriff soldaten forschung zeitalter` (neu `speichern`: mitten im Bau speichern, laden, beide Welten 100 s weiterlaufen lassen — Haus zur selben Zeit fertig, Holz und Lage aller Dorfbewohner gleich; Kamera auch im doppelt so großen Fenster; `menue`: „Spiel laden“ nur mit Spielstand) · `checks/sv_menue.py` · `checks/sv_laden.py gl|dx` (Spielstand im echten Spiel per `--laden`) · `start_rts.py gl|dx` · Fotos aus der Scratchpad-Kopie (Speichern über das Esc-Menü, Laden über das Hauptmenü, Spielstand in den Scratchpad umgelenkt): geladenes Spiel deckt sich mit dem Bild vor dem Speichern bis auf Gras- und Figurenanimation.
**Offen:** Maus und Tasten im echten Spiel (die Fotos lösen die Menüpunkte per Reflection aus); mehrere Spielstand-Plätze sind seit SV5/SV6 da (Plätze löschen noch nicht); bei einer künftigen Formatänderung `SaveGame.CURRENT_VERSION` erhöhen und alte Stände übersetzen statt ablehnen.

Davor: **Menüs scharf statt verschwommen** (2026-10-14) — Feedback: „wenn ich im spiel esc drücke ist der Text verschwomen und animiert“ (Bild in `errors/`).
Ursache: `MenuScreen` (Pause, Einstellungen, Über) und `MessageBoxScreen` zeichneten im 800×480-Raster mit der 14-pt-HUD-Schrift, die `GlobalTransformation` auf Fenstergröße streckte (beim Nutzer ×5,6); dazu pulsierte der gewählte Eintrag (`MenuEntry.Draw`, Sinus auf die Skalierung) und das Menü glitt seitlich herein.
Neu: beide zeichnen in echten Fensterpixeln im Stil des Hauptmenüs — Tafel mit Goldrand, Einträge als Platten, gewählter Eintrag gold umrandet, nur noch Einblenden (0,25 s), kein Pulsieren. Gemeinsame Helfer in `Screens/MenuStyle.cs`; `MenuEntry` ist nur noch Text + Enabled + Event. Maus wie im Hauptmenü (Zeigen wählt, Klick startet), Trefferflächen in Fensterpixeln statt über `CurrentCursorLocation` (800×480). Schrift: `ScreenManager.MenuFontFor(pixelHöhe)` wählt 40 pt (`Fonts/Menu`) oder die neue 80 pt (`Fonts/MenuLarge`), damit sie verkleinert statt gestreckt wird; das Hauptmenü nutzt das jetzt auch (sein Titel war auf 4K ebenfalls ×3 gestreckt). Einstellungen: „Sprache“ und „Partikeleffekt“ entfernt (Wunsch des Nutzers); die gespeicherte Sprache gilt beim Start weiter, die Partikelvorschau im Einstellungsbildschirm ist weg.
Abnahme: Build WindowsDX (0 Fehler, `MenuLarge.xnb` gebaut) · Fotos aus einer Scratchpad-Kopie mit Render-Target-Patch bei 4812×2707 (DPI-bewusst wie beim Nutzer) und 2406×1353: Pause, Beenden-Abfrage, Einstellungen (12 Zeilen passen), Hauptmenü — Schrift im 1:1-Ausschnitt scharf. **Offen:** Mausbedienung im echten Spiel (Fotos zeigen nur das Bild).

Davor: **Einstellungen: die 10 wichtigsten Spiel-Optionen** (2026-10-14) — Feedback: „ich würde gerne mehr option in der Einstellungen anbieten die wir auch aktuell als variable im code setzten können. So die 10 wichtigsten“.
Hauptmenü → Einstellungen hat jetzt 10 weitere Einträge; jeder blätter bei Klick durch feste Stufen, der Wert wandert in settings.json (SettingsManager, wie Karte/Sprache). Alle zehn waren bisher hartkodiert; der Default ist exakt der alte Wert, also ändert sich ohne Aktion nichts:
1. **Lautstärke** (0/25/50/75/100 %, Default 60) — war `SoundBoard.VOLUME = 0.6`; neu setzbar `MasterVolume`, `Play` multipliziert damit. Die Alarm-Sound beim Angriff bleibt laut (eigener Pfad) — so gewollt.
2. **KI-Schwierigkeit** (Leicht/Normal/Schwer/Extrem, Default Normal) — der Screen aktiviert jetzt `EconomyAi(profile)` statt immer Standard; Mapping Easy→`AiProfile.Easy`, Normal→`Standard`, Hard→`Hard`, Extreme→`Extreme`.
3. **Startkapital** (Arm = 100/100/50/100, Normal = bisher 200/200/100/200, Reich = 400/400/200/400) — neu `AoE.Core.Economy.StartingCapital` + `StartingCapitalRules.Values` + `ResourcePool(food,wood,gold,stone)`; der Player-Konstruktor nimmt die Stufe (Zivilisations-Bonus kommt obendrauf, z. B. Briten +50 Holz).
4. **Bevölkerungsgrenze** (100/200/400, Default 200) — `UpdatePopulationLimits` rechnet `Population.Capacity(..., limit)`.
5. **Einheiten-Tempo** (0.5/1/1.5/2×, Default 1×) — Multiplikator im Lauftakt `MovementSpeed × 40 × Pace`.
6. **Sammeln** (0.5/1/1.5/2×, Default 1×) — Multiplikator vor `GatherJob.Update` (Forschungs-Faktoren bleiben erhalten).
7. **Produktion** (1/1.5/2/4×, Default 1×) — Multiplikator für Bauen, Ausbildung, Forschung UND Aufstieg (alle vier `Update`s im Screen).
8. **Kampf-Abstand** (0.25/0.5/1/1.5 Kachel, Default 0.5) — war `MELEE_STOP_TILES` (Konstante bleibt als Default).
9. **Verbands-Abstand** (0.25/0.5/1 Kachel, Default 0.5) — war `COMBAT_SPACING_TILES` (Konstante bleibt als Default; deckt Verbands-Ausweich, `InAttackRange`, Lauf-Verteilung).
10. **Kamera-Pan** (400/800/1600 px/s, Default 800) — war `CAMERA_PAN_SPEED`.
Übernahme: `RTSGameplayScreen.LoadContent` (beim Welten-Aufbau) → `ApplyStoredGameSettings()` (null-safe: Harness ohne ScreenManager/Dienst bleibt bei den Defaults = exakt dem alten Verhalten) → `ApplyGameSettings(settings)` (internal, per Reflection prüfbar). Stufenlisten + Blätter-Regeln in neuem `AgeOfEvolutions.Core.Settings.GameplayOptions`. SettingsScreen: 10 neue `MenuEntry`-Zeilen (vor Vollbild/Sprache/Partikel); der PropertyChanged-Handler speichert und aktualisiert die Texte; die Erstantexte kommen in `LoadContent` (davor ist settingsManager noch null).
Abnahme: `dotnet build` (0 Fehler) · `dotnet test` **310/310** (+8 neue `GameSettingsTests`: Startkapital-Stufen, Briten-Bonus, Population-Limit, Stufen-Blätter, Defaults im Bereich, SoundBoard-Master) · `tools/ai-pruefung` → alle Prüfungen ok (9) · `tools/spielablauf -- bauen soldaten zeitalter forschung farm angriff verteidigung kampfbild zielen zielwechsel bewegen niederlage weiterbauen` → „Alle Abläufe stimmen" (3 Karten). Nebenbei: `AoE.Tests` referenziert jetzt `AgeOfEvolutions.Core` + `MonoGame.Framework.Native` direkt (ohne PrivateAssets — die Runtime-DLL kommt nur ins Test-Output, nicht in die Games).
**Offen: User-Blick** auf die zehn neuen Menüzeilen (visuell) und das Anhören der Lautstärke-Option — das Menü selbst ist headless unsichtbar.

Davor: **Geräusche aus echten Aufnahmen (S4)** (2026-10-13). Feedback: „die generierten sounds passen nicht zu den handlungen. Beispielsweise wenn ein dorfbewohner einen Baum fält sollten geräusche wie holz hacken mit einer Axt zu hören sein“. Die Zuordnung im Code stimmte (`WorkSoundFor`: Holz → `axt`); falsch klang die Synthese selbst — gedämpfte Sinus-Eigenschwingungen klingen nach Holzblock-Instrument, nicht nach Axt im Stamm.
`tools/klang/klang.py` neu: statt Synthese Einzelschläge aus 26 CC0-Aufnahmen von Freesound (alle per Detailseite als `publicdomain/zero` geprüft) — Axt: „Felling a tree with an axe“ (tomattka) und „Axe chopping wood“ (xkeril); Spitzhacke: „Gathering Stone Resources“ (qubodup, ausdrücklich für RTS-Arbeiter an Stein/Gold) und „Stone hit“; Hammer: Nagel in Brett (CapsLok, aidansamuel); Erde: Schaufel sticht in Erde; Busch: „Harvest Sounds“, „Bush Cut“; Schwert: drei Klingenschläge; Treffer: zwei „Thwack“ und „Basic Melee Hit“; Gebäude: „Axe Hits Wood Door“ (drei Schläge); Bogen: zwei Langbogen-Sehnen und „Bow Release“; Tod: drei Körperfälle; Einsturz: drei einstürzende Holzhäuser (3 s). Schnitt ab Einsatz −4 ms (Schlag sitzt am Dateianfang, also auf dem Aufschlag der Animation), Länge unter dem Schlagabstand (`ToolStyles.Rate`), Hochpass 40 Hz, letztes Drittel ausgeblendet, Lautheit wie die alten Dateien (lauteste 50 ms), Begrenzer bis 6 dB, Spitze −1 dBFS. Dateinamen und Anzahl gleich (11 × 3) — kein C#-Code geändert. Vorschau zum Anhören: `klang.py --vorschau` → `tools/klang/ausgabe/vorschau.wav` (47 s, Zeitplan auf der Konsole).
Abnahme: Build DesktopGL und WindowsDX (0 Fehler) · `checks/s_klang.py` (33 gebaut, beim echten Start alle geladen) · je Datei Länge, Pegel und Lage der Spitze gemessen, Spektrogramme gesichtet, ein Klick am Ende von `bogen_2` weggeschnitten. **Offen: anhören** — ausgewählt nach Beschreibung, Spektrogramm und Pegel; gehört hat die Schnitte noch niemand. Gefällt eine Variante nicht, in `SCHNITTE` eine andere Stelle oder Quelle eintragen und `klang.py` neu laufen lassen.

Davor: **Geräusche eingebaut (S1–S3)** (2026-10-11) — Feedback: „die sound effekte sind scheinbar generiert aber noch nicht eingebaut“. Es gab nur den Generator `tools/klang/klang.py`, keine Dateien im Content und keinen Aufruf im Spiel.
(1) **Dateien:** `klang.py` mit der Python von ComfyUI nach `Content/Sounds` laufen lassen — 11 Geräusche × 3 Varianten (`axt`, `spitzhacke`, `hammer`, `erde`, `rascheln`, `schwert`, `treffer`, `gebaeude`, `bogen`, `tod`, `einsturz`), alle 33 in `AgeOfEvolutions.mgcb` eingetragen.
(2) **`Audio/SoundBoard.cs` (neu, S1):** lädt defensiv (fehlende Datei oder kein Audiogerät = stumm), wählt die Variante zufällig, verstimmt um ±0,08 Oktaven, Lautstärke 0,6; Drossel je Geräusch 0,07 s — dreißig Holzfäller ergeben höchstens 14 Axtschläge je Sekunde statt Matsch.
(3) **Werkzeugschläge (S2):** `RTSGameplayScreen.EmitSound(name, worldPos)` spielt nur, was Spieler 0 sieht (Nebel = still); `SoundMix` macht es im Bild voll laut, über 8 Kacheln daneben ausklingend, Stereo-Lage bis 0,7 nach der Bildschirmseite. `UpdateWorkSounds` (in `Update` nach `UpdateUnitMotion`) lässt jedes Werkzeug genau beim Aufschlag der Animation klingen (`StrikeLanded`, derselbe Takt wie `DrawVillager`): Axt am Holz, Spitzhacke an Stein und Gold, Hacke auf dem Feld (`erde`), Sichel am Beerenbusch (`rascheln`), Hammer am Bau; Angel und Fleisch bleiben still (`WorkSoundFor`). `LoadContent` meldet auf der Konsole `[Klang] 33 von 33 Geräuschvarianten geladen`.
(4) **Kampf (S3):** `UnitHitSound` — Fernkämpfer `bogen` (beim Schützen), zwei bewaffnete Nahkämpfer `schwert`, sonst `treffer` (beim Getroffenen); Schlag an ein Gebäude `gebaeude` beim Angreifer; `tod` in `DestroyUnit`, `einsturz` in `DestroyBuilding` (Mitte der Grundfläche).
Abnahme: Build DesktopGL und WindowsDX (0 Fehler) · neue `spielablauf`-Gruppen `klangbrett arbeitsklang kampfklang` (3 Karten; je Treffer genau ein Geräusch am richtigen Ort, Axt- und Hammerschläge im Takt der Animation) · `angriff zielwechsel kampfbild verteidigung werkzeug bauen` ok · `checks/s_klang.py` (Dateien gebaut, beim echten Start alle 33 geladen) · Startprobe gl. Elf eingebaute Fehler (Drosselreihenfolge, Eckabstand, Takt ohne Versatz, Bogen am Ziel, kein tod …) fand die Abnahme alle. **Offen: anhören** — ob Lautstärke, Drossel und Mischung im Spiel angenehm sind, sieht keine Prüfung.

Davor: **Mehrfach-Lauf-Befehl verteilen sich um den Punkt** (2026-10-11) — Feedback: „selektiere ich mehrere leute und gebe einen Punkt an wohin sie gehen sollen, gehen alle an genau den Punkt und man sieht nur den vordersten … verteilen sie sich nicht ein wenig um den angegebenen Punkt herum" + Klarstellung: „nur wen einer selektiert ist soll er genau an den angegebenen Punkt gehen."
`RTSGameplayScreen.IssueCommand(int ownerId, List<Unit>, Vector2 gridPos)` (die Lauf-Abzweigung): `spread = Laufbefehl && Auswahl > 1`. Eine einzelne läuft weiterhin exakt auf `GridToWorld(gridPos)` (unverändelter Pfad). Bei mehreren: jeder bekommt über `MoveStandPoint(gridTarget, index, assigned)` einen eigenen Standplatz — Sonnenblumen-Spirale (goldener Winkel 137,5°, Radius `√index·spacing·1,4`, `spacing = COMBAT_SPACING_TILES·TileSize` ≈ 0,5 Kachel), dazu 2 Runden Mindest-Abstand zu den bereits vergebenen Standpunkten, dazu Begehbarkeits-Check (sonst Nachbar-Scan im 16er-Ring, Fallback = Klickpunkt → `FindPathKnown` liefert leer → `Idle` statt ewig `Moving`). `Unit.State = Idle` bei leerem Pfad (Klick ins Wasser/Ruin) statt Moving.
Abnahme: `dotnet build` Core (0 Fehler) · `dotnet test` (302/302) · `spielablauf bewegen` (3 Karten, inkl. neue Gruppe `verteilen`: v[0] 2 px, Min-Zweier 21 px) · `spielablauf gehen` (einzelner läuft weiterhin exakt auf den Punkt).

Davor: **Zielwechsel beim Soldaten** (2026-10-11) — Feedback: „in der näheren Umgebung stehen noch weitere gegener müssen diese auch automatisch angegriffen werden. Der Soldat darf dann nicht nach dem ersten einfach aufhören solange noch gegner da sind."
`RTSGameplayScreen.DestroyUnit(victim)`: statt dass alle auf `Idle` fallen, holt jeder Angreifer dessen Ziel gestorben ist `NearestLiveEnemy(unit, 6 Kacheln)` — nächsten lebenden Gegner (anderer OwnerId, nicht Dead). Gibt es einen: neues `UnitTarget`, `TargetPosition`, `Path = FindPath(→AttackStandPoint)`, `State = Moving`/`Attacking`. Gibt es keinen: wie bisher Idle. `NearestLiveEnemy` ist `internal`, damit spielablauf die Regel direkt prüfen kann (ohne sie über `DestroyUnit` zu ziehen).
Abnahme: `dotnet build` Core (0 Fehler) · `dotnet test` (302/302) · neue spielablauf-Gruppe `zielwechsel` (3 Karten, alle ok: „A nach 17 s besiegt, Soldat weicht auf B aus, B nach 16 s ebenfalls besiegt").

Davor: **Kampf-Abstand + Bogenschützen-Haltebereich + Verbunds-Abstand** (2026-10-11) — Feedback: „Die Schlachten sind unübersichtlich, weil alle an der gleichen Stelle stehen. Die Soldaten müssen Abstand halten und zum Angreifer blicken. Abstand nicht nur zu den eigenen Soldaten, sondern auch zu den Gegnern — etwa ein halber Soldat sollte zwischen die beiden passen. Auch müssen die Bogenschützen nicht in die Nähe des Gegners; sie haben ja die Bogen — ab einem Häuser Abstand können sie schon angreifen.
`RTSGameplayScreen`: `AttackStandPoint(unit, target)` — der Anhalts-Punkt auf der Linie Ziel→Einheit in Anhalt-Entfernung: Nahkampf **0,5 Kachel** (16 px — Nutzer 2026-10-11: „mach lieber 0.5 Kacheln", davor 1 Kachel → 0,75), Bogenschützen die volle Bogenvorlage (Range 4 Kacheln). Verbands-Ausweich: radial 2 Pässe gegen ALLE anderen Einheiten (eigene + Gegner) mit Mindest-Abstand 0,5 Kacheln (gleiche Größe wie der Nahkampf-Anhalt); die Abweichung wird auf einen Mindestabstand radial begrenzt, damit die Einheit in der (stop + spacing)-Reichweite bleibt und angreifen kann, statt zu pendeln. `InAttackRange`: pixel-genau (Chebyshev) mit `COMBAT_SPACING_TILES`-Aufschlag, keine Kachelrundung — hält auf dem Punkt, angreift in seiner Reichweite. `AttackUnit`: Endpunkt ist der Standpunkt statt dem Ziel; Fallback: wenn die Standpunktkachel nicht begehbar ist (Kartenrand/Steilkante), halbiert der Abstand bis die Kachel frei wird, sonst wäre `Path` leer und die Einheit würde aufgeben. `HandleAttack`: Neu-Pfad führt auch zum Standpunkt. `StepAlongPath`-Repuls: `AttackStandPoint` statt `uTarget.Position` — läuft nicht ins Leere, wenn das Ziel wegläuft.
Blick zum Angreifer: schon vorher da (K7 `UpdateUnitMotion` `UnitTarget`-Zweig). Nicht geändert.
Abnahme: `dotnet build` Core (0 Fehler, isoliert — dein Spielfenster PID 26588 lockt `Core.dll`, daher **Nächster Schritt: `dotnet build AgeOfEvolutions.slnx` nach Schließen des Spielfensters neu ziehen**) · `dotnet test` (302/302) · `spielablauf -- Angriff Soldaten Schlag Zielen Verteidigung Kampfbild Blick` → alle ok (3 Karten; Zielen Karte 1 war zuerst „1 Einheit ohne Ziel", nachdem der Standpunkt auf einer nicht-begehbaren Kachel lag — mit Fallback gelöst). **Achtung: `spielablauf`-Gruppennamen sind exakte Strings nach `ToLowerInvariant`** — kürzere Namen wie `ang`/`ziele` feuern die Gruppen `ang`/ziele` still NICHT ab („Alle Abläufe stimmen" ist dann ein False-Positive). `dotnet run --project tools/ai-pruefung` läuft.

Davor: **Angegriffener Soldat wehrt sich automatisch** (2026-10-11) — Feedback: „wenn ein Soldat angegriffen wird, muss er sich automatisch wehren."
`RTSGameplayScreen.HandleAttack` (die einzige Stelle, an der Einheit-gegen-Einheit-Schaden durchläuft): direkt nach dem eigenen `unit.Attack(target)` schlägt **der** Getroffene zurück, wenn (a) er zuschlagen kann (`AttackPower > 0` — Soldaten UND Dorfbewohner, wie in AoE) und (b) **kein eigenes Ziel** hat (`UnitTarget` und `AttackTarget` beide null, nicht tot). Dann bricht er aus Sammel- oder Baustellung aus und greift den Angreifer an (über die bestehende `AttackUnit`-Logik wie bei der K6-TZ-Defense). Hat er schon ein eigenes Ziel, bleibt das unverändert — kein Abbrechen beim laufenden Kampf. Der Hook deckt alle Angriffspfade ab: Klick-Auswahl, KI-Befehl, K6-TZ-Defense. Nicht ping-pong: wer selbst schon angreift, reagiert nicht auf Gegenschlag, solange er ein Ziel hat.
Abnahme: `dotnet build` Core (0 Fehler, isoliertes Output-Verzeichnis — dein Spiel-Fenster (PID 26588) lockt `Core.dll`, daher **Nächster Schritt: `dotnet build AgeOfEvolutions.slnx` nach Schließen des Spiel-Fensters erneut laufen lassen**) · `dotnet test` (302/302) · `dotnet run --project tools/spielablauf -- ang soldaten verteidigung kampfbild` → alle ok (3 Karten; Verteidigung zeigt: Angreifer nach 13.0 s besiegt, „der Gegner verteidigt ebenso") · `dotnet run --project tools/ai-pruefung` → alle 9 Prüfungen ok (beide KI aktiv, Haus t=1048 s zerstört).

Davor: **Fehlende Rohstoffe beim Bauen benennen statt „Hier kann nicht gebaut werden"** (2026-10-11) — Feedback: „Die Meldung 'hier kann nicht gebaut werden' macht keinen Sinn wenn einfach bestimmte Resourcen fehlen. Hier die Resourcen nennen die fehlen."
`RTSGameplayScreen.PlaceBuilding` (Mensch-Klick, Owner 0) trennt die Abweisungsfälle jetzt:
(1) **Platz nicht frei** (unbebaubare Kachel, eigenes Gebäude/Einheit im Weg, nicht erkundet) oder **Stadtzentrum** (darf nie gebaut werden — `PlaceBuildingFor` verbietet es, es gibt genau EINES): bleibt „Hier kann nicht gebaut werden".
(2) **Rohstoffe fehlen:** `BuildingRules.CostOf(type)` gegen `player1.Resources.HasEnough(cost)`; jede Ressource, die nicht reicht, wird genannt — Meldung z. B. `Fehlende Rohstoffe: 25 Holz` bzw. `Fehlende Rohstoffe: 100 Gold + 50 Stein` (Namen über die vorhandene `ResourceName`: Nahrung/Holz/Gold/Stein). Der Setzmodus bleibt an wie bisher.
(3) Platz + Rohstoffe ok: weiter an `PlaceBuildingFor` wie vorher. Die KI-Seite (Owner 1) meldet nichts — sie baut nur, was sich leisten kann.
Abnahme: `dotnet build` Core (0 Fehler, isoliertes Output-Verzeichnis) · `dotnet test` (302/302) · kompletter App-Build + spielablauf-Lauf wartet, bis das laufende Spielfenster (PID 26588) die Debug-BINs freigibt — **Nächster Schritt: `dotnet build AgeOfEvolutions.slnx` erneut laufen lassen**, `dotnet run --project tools/spielablauf -- bauen weiterbauen` für den Bauplatz-Lauf.

Davor: **Kartenformat pro Fenster + schwarzer Hintergrund hinter der Karte** (2026-10-11) — Feedback: „beim Karten Format muss noch beachtet werden, dass oben und Unten Menübalken sind… Diese müssen beim Berechnen des Kartenformats berücksichtigt werden. Zudem soll der Hintergrund, der grün ist hinter der Karte, schwarz sein." Finale Klarstellung des Nutzers: **das Kartenformat = Fensterbreite ÷ Fensterhöhe**; die Leisten fliegen NICHT in die Kartenform ein — berücksichtigt werden sie durch die Kameraspannung zwischen ihnen und den schwarzen Rand.
(1) **Kartenformat:** `RTSGameplayScreen.LoadContent` rechnet das Kartenformat aus `screenBounds.Width / screenBounds.Height` (Fenster, Leisten nicht abgezogen). `Kartengrößen` (spielablauf) nutzt dieselbe Formel. Die *Kamerazentrierung* (`ClampCamera`) hält die Karte trotzdem im freien Streifen ZWISCHEN den Leisten: ist sie höher, eingespannt (Kartenoberkante höchstens y=56, Kartenunterkante mindestens y=H−200); ist sie kürzer, vertikal in den Streifen zentriert.
(2) **Schwarzer Hintergrund:** `RTSGameplayScreen.Draw` — `graphicsDevice.Clear(Color.Black)` statt `Clear(new Color(22,79,45))`. Alles außerhalb der Kartenfläche (oben über/unter die obere Leiste, links/rechts neben ihr, die Lücken wenn die Karte kleiner als das Spielfeld ist) bleibt jetzt schwarz. Das Bodenshader-Kontrollbild (`DrawGroundShaded`) zeichnet exakt die Kartenfläche und die `px`/Texturen der HUD-Leisten überstreichen die Leisten — der schwarze Clear hinterlässt nur die Ränder.
Abnahme: `dotnet build` (0 Fehler) · `dotnet test` (302/302; vorherer Stand 278, +24 neue in EconomyAiTests) · `tools/ai-pruefung` → **alle 8 Prüfungen ok** (beide KI aktiv, erkunden, Angriff, Haus bei t≈958 s zerstört) · `tools/spielablauf -- karten zoom minimap linksklick auswahl` → alle ok (3 Karten) · `tools/kartenpruefung -- wild rohstoffe start groessen` meldet einen Gold-Dichte-Verstoß auf der 32×32-Karte, der VOR meiner Änderung existiert und nicht durch sie verursacht ist (das Tool erzeugt Karten direkt mit `TileMap(...)` und berührt weder LoadContent, ClampCamera noch Clear) — abhaken: `Standard: Gold 255% so dicht wie auf der Standardkarte`.

Davor: **Gegenwehr/K8-K9/AiBridge/Feature-Notes/Einzel-Features (K5, K6, K7, Entwicklung)** (2026-10-11) — 15 unstaged + 6 staged Dateien, dokumentiert unter `FEATURENOTES-2026-10-11.md` am Repo-Root. Kurz: (1) KI wehrt sich gegen Angreifer auch fern vom TC (K3), (2) Scout erkennt Gegner-Nebel-Kacheln (K8), (3) Angriffswelle nimmt das erste Gebäude in Sichtweise (K9), (4) Gegner anklicken / TC-Verteidigung / Schlagbild bei Einheiten (K5/K5b/K6/K7) (RTSGameplayScreen), (5) Gold in der Dunklen Zeit + Farm-Schwelle = Aufstieg+100 (EconomyAi), (6) `Directory.Build.props` + `Modelfile` (Build/Infra), (7) `tools/ai-pruefung/Entwicklung.cs` + `Gegenwehr.cs`, `tools/klang/klang.py`.
Abnahme: `dotnet build` (0 Fehler) · `dotnet test` (302/302) · `tools/ai-pruefung` → alle Prüfungen ok · `tools/spielablauf -- zielen verteidigung kampfbild` → ok.

Davor: **Unter-Angriff-Erkennung, automatisch zurückschlagen, Alarm** (2026-10-10) — Feedback: der gegnerische Dorfbewohner marschierte bis ins eigene Dorf, ohne dass die KI reagierte; dazu: Sound + oberer Balken kurz rot bei Angriff auf unsere Seite.
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
2. ~~**Feuer in der Schmiede animieren**~~ — erledigt (A5, 2026-10-20): Feuer flackert, Funken,
   Kaminrauch aus Haus und Schmiede. Siehe C6f.
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
    Seit RT (2026-10-23) dazu **Bären** (2–3, Einzelgänger, 80 LP, erlegt 400 Nahrung) und
    **Wolfsrudel** (2–3 je 2–3, 20 LP, ohne Nahrung): sie greifen an, wer ihnen zu nahe kommt,
    verfolgen niemanden und halten 5 Kacheln Abstand zu Gebäuden (Gruppe `raubtiere`).

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
| `tests/AoE.Tests` | net10.0 | **394/394 grün** (2026-10-16) | Einheiten und Ressourcen (15), Sammelauftrag (21), Ausbildung und Bevölkerung (22), Bauen (74), Zeitalter (46), Forschungen (43), Konter-Dreieck (5), Gebäudekampf (9), Wegfindung (3), Nebel (8), Wirtschafts-KI (20), Fehlersuche (1) - so weit Stand 267; seither u. a. Einstellungen, Spielstand, Reparatur (11) und Wege (20) |
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
- [x] `git rm -r --cached` für `bin/`, `obj/`, `.vs/`, `Content/obj/`, `Content/bin/`
      — vom Nutzer ausgeführt (geprüft 2026-10-23: `git ls-files` kennt keine
      `bin/`- oder `obj`-Datei mehr)
      — **bewusst nicht automatisiert.** Das stellt 501 Löschungen in den Index;
      dieser Schritt gehört dir, nicht einem Agent. Per Trockenlauf geprüft, erfasst
      genau diese 501 Dateien und keine Quelldatei, in PowerShell wie in der Git-Bash:
      `git rm -r --cached -- ':(glob)**/bin/**' ':(glob)**/obj/**' ':(glob)**/.vs/**'`
      Der früher hier notierte Befehl mit `AgeOfEvolutions/*/bin` erfasste `src/`,
      `tests/`, `demo/` und `Content/` nicht und bricht in der Git-Bash mit
      „pathspec did not match" ganz ab.
- [x] Abnahme: `git status --short` zeigt nur noch echte Quelldateien (geprüft 2026-10-23)

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
- [x] Auf echter macOS-Hardware spielen — auf Apple Silicon (osx-arm64) startet `--rts`
      und läuft ohne Ausnahme; das Spielen selbst bestätigt *(Nutzer, 2026-10-22)*

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
- [x] **Verhaltensänderung:** Startkapital kommt jetzt aus `ResourcePool`
      (200/200/100/**200**). Das Spiel begann vorher mit 100 Stein. Falls du die
      alten 100 willst, muss `ResourcePool.START_STONE` geändert werden.
      (angenommen 2026-10-23: 200 sind durch `GameSettingsTests` fixiert)

### B2 · `DamageCalculator` verdrahten [X]

- [x] `Unit.DamageAgainst()` und `Unit.Attack()` rechnen über `DamageCalculator`
- [x] Konter-Dreieck steckt jetzt in den Klassenboni statt im pauschalen Faktor 2
- [x] Abnahme: `tests/AoE.Tests/CounterTriangleTests.cs`, 5 Tests
- [x] Nebenbefund behoben: die Werttabelle deckte nur 8 der 17 Einheitentypen ab —
      die übrigen 9 kamen mit **0 Lebenspunkten** zur Welt. `CoreUnits.Create`
      bildet jetzt alle 17 ab.
- [x] Gegenstandslos (geprüft 2026-10-23): die Kampfschleife existiert — Einheiten
      greifen über `AttackUnit` an (Klickbefehl, K6-Verteidigung, Auto-Antwort,
      KI-Welle), Gebäude über `BuildingCombat.Hit`

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
- [ ] Jedes Zeitalter schaltet Gebäude und Einheiten frei — die Regel steht
      (`AgeRules.RequiredAgeOf`, Baumenü und Tasten richten sich danach). Erstes
      Gebäude der Feudalzeit: **Wachturm** (Taste T, 50 Holz + 125 Stein, 80 s, sieht
      10 Kacheln weit) *(Agent C4c)*. Offen: weitere Gebäude und die Einheiten — die
      brauchen die zurückgestellte Kampfschleife. Stand 2026-10-23: alle Gebäude stehen nach
      Zeitalter im Baumenü (L2); offen sind die Einheiten - siehe F2, F14, F15
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

- [x] Ressourcenleiste oben: `[Holz] [Nahrung] [Gold] [Stein] [Bev. 37/45] « Feudalzeit »` *(erledigt, bestätigt 2026-10-16)*
- [x] Bevölkerung rot bei erreichtem Limit *(Agent C2b)*
- [x] Kommandoleiste unten: Aktionssymbole – Einheiteninfo – Minimap *(erledigt, bestätigt 2026-10-16)*
- [x] Befehlstasten mit Symbolen (Qwen-Image, `tools/bilder`); Name, Kürzel und Kosten
      nennt die Leiste, solange die Maus auf einer Taste steht *(Agent C6i)*
- [x] Leisten im Stoff des Zeitalters: rohe Holzbohlen, Eichenbohlen, Quadermauer, grüner
      Marmor mit Goldkante (`PanelAssets`, `DrawPanel`); dunkles Feld hinter dem Hilfetext
      *(Agent H1)*
- [x] Rohstoffsymbole oben als Bilder, doppelt so groß (`ResourceIcons`) *(Agent R1)*
- [x] Minimap: kleine Karte rechts unten in der Kommandoleiste, in derselben Ansicht wie
  die Spielkarte (zurzeit Draufsicht, Norden oben; Entscheidung 2026-10-03, statt
  der Raute aus dem AoE-II-HUD) — Farben nach dem Referenzbild `docs/overview.jpg`:
  Grün Land, Braun Wald, Blau Wasser, weiße Punkte eigene Einheiten, dunkler Rahmen.
  Feinde rot, Kameraausschnitt als Rechteck *(Agent C6m, C6d; bestätigt 2026-10-16)*

### C7 · Terrain-Rendering vervollständigen [R]

- [x] Nebel des Krieges: unerforscht schwarz, erforscht abgedunkelt, fremde Einheiten
      nur in Sicht; ein Klick in den Nebel ist ein Laufbefehl *(Agent C7n)*
- [x] Baustellen decken keinen Nebel auf, Sicht spendet erst das fertige Gebäude
      (`BuildingEntity.IsUnderConstruction`) *(Agent C7b)*
- [x] Bäume als prozedurale Pixel-Art — bestand schon (`BuildForestTexture`, `DrawTree`)
- [x] Beeren, Stein, Goldminen als Textur statt Farbfläche — bestand schon
      (`BuildBerryTexture`, `BuildMountainTexture`, `BuildGoldTexture`)
- [x] **Schatten** — Schlagschatten wie Sonnenschatten: jedes stehende Objekt wirft seine
      Silhouette nach rechts vorn, direkt vom Bodenkontakt aus, geschlossen, ohne Doppeltdunkel,
      kühl getönt; Bäume, Figuren und Tiere als Körper (`RTSGameplayScreen.Schatten.cs`, Gruppe
      `schatten`, `checks/c7_schatten.py`, Vorbild `errors/Schatten.jpg`) *(Agent C7sch3a–f, C7sch4, 2026-10-22)*
- [ ] Figuren und Grasbüschel im Schatten abdunkeln (sie liegen über dem Schattenbild)
      **Beleuchtung: bewusst nicht gemacht** — die Qwen-Bilder haben ihr Licht eingebrannt,
      eine eigene Richtungsschattierung oder ein Tag-Nacht-Ton würde dem widersprechen
      (wie in AoE 1/2); Ausnahme höchstens ein globaler Farbton, und der erst nach Sichtprüfung.
- [x] Stadtzentrum skaliert im Ganzen mit dem Zoom *(Agent E9)*
- [x] Gebäude als Sprites aus Qwen-Image (`tools/bilder`, freigestellt mit BiRefNet), Fahnen
      und Banner in Spielerfarbe; Baustellen und die Farm zeichnet weiter der Code *(Agent C6g)*
- [x] Gebäude bewegen sich: das Windrad der Mühle dreht sich (Qwen-Image hat die gemalten
      Flügel aus dem Mühlenbild entfernt, Gebaeude/muehle_ohne, das Flügelkreuz
      Gebaeude/muehle_fluegel dreht der Code um die Nabe), und die Fahnen auf Haus, Mühle,
      Wachturm und den Lagern wehen - das Tuch in Streifen, die eine Welle hebt und
      senkt (DrawWavingFlag, FlagWave) *(Agent C6a)*
- [x] **C6f Feuer in der Schmiede** — erledigt als A5 (2026-10-20, siehe oben), dazu Kaminrauch
      aus Haus und Schmiede, je Gebäude genau ein Kamin *(Agent A5)*. Ursprünglicher Hinweis
      (Nutzer, 2026-10-07): das Feuer der
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
- [x] Boden im Nebel: Lebendbild und Grasbüschel zeigen den zuletzt gesehenen Stand - ein
      unsichtbarer Gegner, sein Weg oder ein Tier ändert dort nichts, bis wieder jemand
      hinsieht (`UpdateGroundMemory`, `SeenTallGrassAt`; Gruppe `nebelboden`) *(Agent G14)*

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
- [x] *Gegenstandslos seit C11r und den Kartengrößen vom 2026-10-23 (Standard 90×90 bis
      World 181×181, im Seitenverhältnis des Bildschirms):*
      **Offene Entwurfsfrage:** die Karte ist 64×64 Kacheln à 32 px = 2048 px,
      ein typisches Fenster ist breiter. Bei Zoom 1 passt die ganze Karte ins
      Bild und wird jetzt zentriert — links und rechts bleibt Rand. Entweder
      größere Karte, oder MIN_ZOOM dynamisch so setzen, dass die Karte das
      Fenster immer füllt (schließt dann aber die Gesamtübersicht aus)
- [x] **C11r Rechteckige Karte** (Hinweis des Nutzers, 2026-10-07) — erledigt, bestätigt 2026-10-16:
  die Karte hat Breite und Höhe je Kartengröße im Seitenverhältnis des Bildschirms bei
  gleicher Fläche wie die quadratische Referenz (`MapSizes.Dimensions`); Startplätze,
  Kartengenerator, Minimap und Kamera funktionieren damit. Abnahme: `tools/kartenpruefung -- groessen`,
  `tools/spielablauf -- kartengrößen`
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

- [x] Einheit sucht nächste Ressourcenquelle und sammelt - Dorfbewohner setzen selbst fort
      (C1), die Wirtschafts-KI verteilt ihre Sammler (`EconomyAi`, K3-K9; geprüft 2026-10-23)

### C12 · Wege: Trampelpfade werden beim Zeitaufstieg besser [R]

Konzept (Korrektur des Nutzers): keine manuellen Straßen. Die Trampelpfade der
Dunklen Zeit werden beim Aufstieg automatisch zu Wegen, mit jeder Zeit besser,
und Einheiten laufen darauf 20 % schneller.

- [x] `Tile.RoadLevel` (0–3) statt `IsRoad`/`RoadProgress`; `TileMap.LevelOfAge`
  (Dunkel 0, Feudal 1, Ritter 2, Imperial 3) und `TileMap.UpgradeRoads(level)`:
  alle vorhandenen Tritte (`Wear > 0`) und früheren Stufen steigen auf, nichts fällt ab
- [x] Neue Tritte (je Kachel-Betritt in `StepAlongPath`, `TileMap.Trample`)
  entstehen ab der Feudalzeit direkt auf der erreichten Stufe
- [x] 20 % Laufbonus auf Kacheln mit `RoadLevel >= 1` (`StepAlongPath`), in der
  Dunklen Zeit (Stufe 0) gleich schnell wie die Wiese
- [x] Bildschirm: `UpdateAges` ruft beim Aufstieg `UpgradeRoads` auf (beide Spieler)
- [x] Drei prozedurale 32×32-Wegetexturen je Stufe (Feldweg mit Kiesel, befestigt mit
  festem Rand, Koppelpflaster auf Mörtel), optional `Boden/wege_feld`,
  `Boden/wege_befestigt`, `Boden/wege_pflaster`; Bodendurchlauf wählt nach `RoadLevel`
- [x] Weg-Stufe in Spielstand (`TileSave.RoadLevel`, Mapper beide Wege)
- [x] Abnahme: Gruppe `strassen` in `tools/spielablauf` — Läufer auf drei parallelen
  11er-Reihen: Dunkle Zeit gleich schnell (10,1/10,0 s), nach Aufstieg auf die
  Feudalzeit der Pfad ~20 % schneller (8,4 s), neue Tritte ab Feudal sofort Stufe 1,
  Stufen steigen 1 → 2 → 3; dazu 6 neue Tests in `tests/AoE.Tests/FeldwegeTests.cs`
- [x] Aufräumen: `BuildingType.Road`, Taste D, `Unit.RoadCell`,
  `TileMap.StartRoad/TickRoad/ActiveRoads/IsActiveRoad`, `MovementCost`-A*-Kosten und
  das kachelweise Straßenbauen sind wieder entfernt
- [x] Seit C16: die Wege kommen aus dem Bodenshader mit Qwen-Image-Bildern je Zeitalter
  (`Boden/weg_feudal`, `weg_ritter`, `weg_imperial`); die prozeduralen 32×32-Bilder sind nur
  noch Ersatz, und Gebäude bekommen ab der Feudalzeit ihren Weg selbst (siehe C16)

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
- [x] Dorfkerne und Kasernen sind wie das Start-Stadtzentrum auswählbar (Klick) und
  bilden Dorfbewohner aus. Stand B1/P1: jedes eigene Stadtzentrum ist auswählbar und bildet
  Dorfbewohner aus, die Kaserne die Miliz (gegenstandslos geprüft 2026-10-23:
  `Products` bildet je Stadtzentrum Dorfbewohner aus, Kaserne Miliz)
- [ ] **Mauern** ab der Feudalzeit baubar: `StoneWall` (Bau `PalisadeWall` ist im
  `BuildingType`-Enum schon da, aber nicht verdrahtet), mit **Stein** kachelweise bauen
  wie Straßen (C12: `AgeRules.RequiredAgeOf`, Baumenü, Taste). Mauern sind für
  **jeden** unpassierbar — Dorf- wie Feindbewohner laufen nur drum herum.
  Stand L2: Palisadenmauer (Taste P, ab der Dunklen Zeit, 2 Holz) und Steinmauer (W, ab der
  Feudalzeit, 5 Stein) stehen im Baumenü, je Kachel ein Stück; nach einem Stück bleibt der
  Setzmodus an. Offen ist die Abnahme `mauern` (Umlaufen, Abreißen)
  — Regel Teil erledigt (geprüft 2026-10-23: beide Typen im Baumenü, Kosten und
  Zeitalter in `AgeRules`/`Construction`; Abnahme bleibt den beiden Kästen darunter
  vorbehalten)
- [ ] Mauer ist zerstörbare Struktur (wie Gebäude HP aus dem `BuildingType`-Werten):
  nur ein angreifender Feind, der ein Stück Mauer **zerstört**, kommt durch; nach
  Zerstörung ist die Kachel wieder begehbar
- [ ] Abbauen eigener Mauern: ein eigener Dorfbewohner kann (wie bei Gebäuden)
  ein eigenes Mauerstück **zerstören** — Taste/Befehl auf das Mauerstück, dann
  wird es Stück für Stück abgerissen und die Kachel ist wieder frei; die eigene
  Mauer ist danach für niemanden mehr eine Barriere. Abnahme in `tools/spielablauf`,
  Gruppe `mauern` (bauen, Feind läuft an → muss umlaufen; eigener Dorfbewohner
  reißt ein Stück ab → die Lücke ist für beide Seiten frei durchgängig)
- [x] Ausgebildete Dorfbewohner stehen **neben dem ausgebildeten Gebäude**, nicht auf
  ihm - `SpawnCell` in `UpdateTraining` (geprüft 2026-10-23)
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
- [x] Die KI greift Gebäude an - ihre Welle zieht auf das nächste sichtbare feindliche
      Gebäude (`EconomyAi.Raid`, K9; geprüft 2026-10-23)

### C15 · Reparatur: beschädigte Gebäude wiederherstellen [R]

Wunsch des Nutzers (2026-10-16): „Angegriffene gebäude sollten repariert werden können.
Wenn sie zBs zu 50% angeschlagen sind müssen auch nur 50% der Rohstoffe verwendet werden
um sie wieder herzustellen.“

- [x] Kosten anteilig zum fehlenden Schadensvolumen: `BuildingRules.RepairCostOf`
  (Baupreis × fehlender Anteil, je Rohstoff mindestens 1; unverletzt = 0)
- [x] Dauer anteilig: `BuildingRules.RepairSecondsOf` (Bauzeit × fehlender Anteil)
- [x] `AoE.Core.Economy.Repair` — Auftrag mit Sekunden, fehlenden LP und Fortschritt,
  SpeedFactor(n) wie am Bau; `ToState`/`FromState` für den Spielstand
- [x] `Building.Repair` (Data), `BuildingSave.Repair`, `SaveGameMapper` beide Wege
- [x] Befehl: Linksklick auf ein eigenes, beschädigtes, fertiges Gebäude mit
  ausgewählten Dorfbewohnern schickt sie zur Reparatur; Kosten einmalig vor dem Start,
  Dazukommende frei; ohne Arbeiter steht der Auftrag still
- [x] `UpdateRepair` im Tick; fertig bei voller Standfestigkeit, Arbeiter weiter oder untätig
- [x] Zustandsmaschine: Ankunft und `Building`-Zweig auch für `IsComplete`-Gebäude
  mit laufender Reparatur (vorher: Arbeiter wurden sofort wieder abgewiesen)
- [x] Leiste zeigt „Reparatur p %"; Meldungen für Auftrag, Kosten und Fehlbeträge
- [x] Abnahme: `reparatur` in `tools/spielablauf` — 3 Zufallskarten grün: 50 %
  → 13 Holz und 12,5 s (1 Arbeiter), 9,6 s (2 Arbeiter), unverletzt nichts
  (geprüft 2026-10-23: Gruppe erneut ausgeführt, 3 Karten grün)

### C16 · Wege von Gebäuden und Wegbilder je Zeitalter [R]

Wunsch des Nutzers (2026-10-16): Trampelpfad in der Dunklen Zeit, danach Pflasterstraße
(Stein, Zement), dann Erde, Schlamm und Kies, am Ende verdichteter Schotter - als Bilder
in hoher Qualität; ab der zweiten Epoche führen von den Häusern automatisch Wege weg.

- [x] Wegbilder aus Qwen-Image (Gruppe `wege`): `Boden/weg_feudal` (Seed 5801, 664 px,
  `"kachelbar": "naht"`), `Boden/weg_ritter` (5781), `Boden/weg_imperial` (5761)
- [x] `tools/bilder/naht.py`: nahtlos kachelbar per Image Quilting, Schnitt in den Fugen
- [x] Bodenshader: `RoadSurface`, `SoilCover`, `RoadTexture`/`RoadOn`/`RoadScale`; Wege
  ausgefranst statt als Kachelquadrate, schärferer Rand als ein Trampelpfad *(Agent W1a, W1b)*
- [x] Neue Bildnamen, Ersatzbilder in der Reihenfolge der Zeitalter ohne Gitterrand *(Agent W1c)*
- [x] GL-Fassung zeigte eine schwarze Spielfläche (Sandbild an zwei Samplern, danach Nebel) -
  immer ein echtes Wegbild binden *(Agent W1d)*; eigentliche Ursache und allgemeine Abhilfe
  in C16b: `ReleaseTextureUnits` nach jedem eigenen Shader-Durchgang *(Agent W3c)*
- [x] C16b: Wege als Linien in eigenem Shader `Effects/Wege.fx` statt Flecken im Bodenshader,
  je Zeitalter Pflaster mit Randsteinen, Erdweg mit Fahrspuren, gewölbter Schotter *(Agent W3c, W3d)*
- [x] C16b: Wegkarte mit Nebel-Gedächtnis, schräge Lücken aufgefüllt (`RoadDrawnAt`,
  `RoadDrawData`) *(Agent W3b)*; Weg erst ab sechs Tritten (`ROAD_WEAR_MIN`) *(Agent W3a)*
- [x] `TileMap.CanCarryRoad`, `TileMap.ConnectBuilding` (höchstens `ROAD_CONNECT_MAX_TILES`
  = 30 Kacheln, seit C16c ohne Höchstlänge) *(Agent W2a, W2a2)*
- [x] Anschluss beim Fertigbau ab der Feudalzeit des Besitzers und beim Aufstieg für alle
  vorhandenen Gebäude; Mauern ohne Weg *(Agent W2b)*
- [x] `CurrentRoadLevel` nach dem Laden aus den Zeitaltern (vorher 0 - Lücke aus C12) *(Agent W2a)*
- [x] C16c: Wege von allen Gebäuden bis ans Stadtzentrum, ohne Höchstlänge; vorhandene Wege
  mitbenutzt (`ROAD_NEW_TILE_COST`), schräg auf das Zentrum zu; Stadtzentrum ohne Weg *(Agent W4)*
- [x] Abnahme: 13 neue `FeldwegeTests`, Gruppe `gebaeudewege`, `checks/c16_wegbild.py`, Fotos
- [ ] Sichtprüfung durch den Nutzer im echten Spiel (Maßstab der Steine, Helligkeit, Wegbreite)

---

# Block F — Lücken gegenüber `docs/AgeOfEmpires.md` (Abgleich 2026-10-23)

Kapitel für Kapitel gegen den **Code** abgeglichen (nicht gegen frühere Doku): was die Spec
beschreibt und im Spiel fehlt oder nur als Hülle existiert. Reihenfolge = Priorität fürs
Spielgefühl - zuerst, was schon baubare Gebäude und vorhandene Tasten mit Sinn füllt.

**Schon da und hier nicht aufgeführt:** Dorfbewohner-Loop mit Traglast, Abgabestellen und
Leerlauf-Taste (`.`); vier Ressourcen, Gold und Stein endlich; Bevölkerungslimit mit Häusern
(100/200/400 wählbar), Produktion stoppt am Limit; vier Zeitalter mit Kosten und Dauer nach
AoE II; alle 19 Gebäudetypen baubar (L1/L2); Bauen mit abnehmendem Ertrag `(n + 2) / 3`;
Reparatur; Schadensformel mit Angriffs- und Rüstungsklassen (`DamageCalculator`); Nebel mit
drei Zuständen und Boden-Gedächtnis (G14); A*-Wegfindung, Verband verteilt sich ums Ziel;
dreigeteiltes HUD mit Minimap; faire Startressourcen je Spieler (`PlaceStartResources`);
Wirtschafts-KI mit Angriffswellen auf sichtbare Gebäude (K9).

### F1 · Türme, Stadtzentrum und Burg schießen [X] — umgesetzt 2026-10-23 (oben)

Stand: Kein Gebäude schießt. Der Wachturm (50 Holz + 125 Stein) bringt nur Sicht, das
Stadtzentrum ruft bei Angriff nur Soldaten herbei (K6, `DefendTownCenters`), die Burg tut nichts.

- [x] Pfeilangriff für Wachturm, Stadtzentrum und Burg: Reichweite, Takt, Schaden über
      `DamageCalculator` (Fernkampf gegen Fernkampf-Rüstung), Ziel = nächster sichtbarer Feind
- [x] Pfeile mit der Flugbahn aus A3 (`MaybeLaunchArrow`) zeichnen, Geräusch wie beim Bogen
- [x] Befiederte Pfeile und Maurerkunst wirken mit; KI greift Türme gezielt an oder meidet sie
      (gemieden: `EnemyInfo.IsArmed`; die Welle zieht auf bewehrte nur, wenn nichts anderes sichtbar ist)
- [x] Abnahme: neue Gruppe in `tools/spielablauf` (Feind im Radius nimmt Schaden, außerhalb nicht)
      — Gruppe `beschuss`

### F2 · Konter-Einheiten: Speerkämpfer, Plänkler, Ritter [X]

Stand: ausbildbar sind nur Dorfbewohner, Miliz, Bogenschütze und Späher (`Products`).
`UnitType` kennt SpearMan, Skirmisher, Cavalry, CamelRider, CavalryArcher u. a., aber ohne
Ausbildung und ohne Bilder. Ohne sie läuft das Konter-Dreieck der Spec ins Leere, und die
goldfreien Einheiten fürs Endspiel fehlen.

- [ ] Werte und Boni in AoE.Core: Speerkämpfer +15 gegen Kavallerie, Plänkler +3 gegen
      Bogenschützen, Ritter 100 LP / 10 Angriff / 2/2 Rüstung (Richtwerte der Spec)
- [ ] Ausbildung: Kaserne Speerkämpfer (Feudal), Schießstand Plänkler (Feudal), Stall Ritter (Ritterzeit)
- [x] Bilder je Spielerfarbe und Zeitalter: Stand, Lauf, Schlag (Qwen-Image, `tools/bilder`, Seitenansicht)
      — `Einheiten/speerkaempfer`, `plaenkler`, `ritter` (je Stand + 8 Lauf, Speer und Ritter + 8 Schlag);
      ein Aussehen je Einheit wie bei Miliz und Bogenschütze, der Plänkler schießt aus dem Standbild.
      Noch nicht in `SoldierSprites` eingetragen (Maßstab Ritter 1,4)
- [ ] KI stellt ihre Wellen nach dem zusammen, was sie vom Gegner sieht
- [ ] Abnahme: Kampfgruppen in `tools/spielablauf` - Speer schlägt Ritter, Plänkler schlägt Bogen,
      Miliz schlägt Speer

### F3 · Aufstieg braucht zwei Gebäude des laufenden Zeitalters [R]

Stand: der Aufstieg kostet nur Rohstoffe und Zeit (`AgeRules.CostOf`). AoE II verlangt dazu
zwei fertige Gebäude des laufenden Zeitalters (Dunkle Zeit z. B. Mühle, Lager, Kaserne).

- [ ] Regel in `AgeRules` (AoE.Core) mit Tests; welche Gebäude zählen, je Zeitalter
- [ ] Taste A gesperrt mit Hinweis in der Leiste, was noch fehlt
- [ ] KI (`EconomyAi`) baut die Voraussetzungen vor dem Aufstieg; Spielstände bleiben ladbar

### F4 · Auswahl wie in AoE: Kontrollgruppen und Doppelklick [R]

Stand: Rechteck-Auswahl und Leerlauf-Taste gibt es; Strg+Zahl und Doppelklick fehlen ganz.

- [ ] Strg+1…9 legt eine Gruppe an, 1…9 wählt sie, zweimal drücken holt die Kamera hin
- [ ] Doppelklick wählt alle sichtbaren eigenen Einheiten desselben Typs
- [ ] Vorher prüfen, dass die Zifferntasten nirgends belegt sind (Baumenü nutzt Buchstaben)

### F5 · Wirtschaftstechnologien vervollständigen [R]

Stand: von den Wirtschaftsforschungen der Spec gibt es Webstuhl, Pferdekummet, Doppelaxt und
Goldbergbau. Es fehlen Schubkarre und Handkarren (Stadtzentrum: schneller laufen und arbeiten,
mehr tragen), Schwerer Pflug und Fruchtwechsel (Mühle, Farmertrag stufenweise), Bogensäge,
Goldschacht sowie Steinbergbau. Die KI forscht gar nicht - `IWorldActions` hat keine Forschung.

- [ ] `Tech`, `TechRules` (Kosten, Dauer, Zeitalter, Gebäude, Vorgänger) und `TechEffects` mit Tests
- [ ] Tasten im jeweiligen Gebäude, Symbole (Qwen, wie die vorhandenen `Icons/`)
- [ ] `IWorldActions.Research` und eine Forschungsreihenfolge in `EconomyAi`

### F6 · Schmiede in drei Stufen und fünf Linien [R]

Stand: je eine Stufe für Nahkampf-Angriff (Schmiedekunst), Fernkampf-Angriff (Befiederte
Pfeile) und Infanterie-Rüstung (Schuppenpanzer). Es fehlen die Stufen 2 und 3 sowie die Linien
Rüstung Kavallerie und Rüstung Fernkämpfer. Spec: +3 Rüstung nimmt 75 % des Schadens.

- [ ] Ketten mit Vorgänger (Stufe 2 Ritterzeit, Stufe 3 Imperialzeit), Wirkung über die
      vorhandenen Angriffs-/Rüstungsklassen, Tests
- [ ] Tastenbelegung der Schmiede bei 15 Forschungen (zweite Reihe oder Blättern)

### F7 · Fremde Gebäude im Nebel im letzten bekannten Zustand [R]

Stand: der Boden zeigt im Nebel den zuletzt gesehenen Stand (G14), Gebäude nicht - alle
Gebäude werden immer live gezeichnet. Ein Gegnerbau im erkundeten Nebel erscheint dadurch
sofort, ein zerstörter verschwindet; genau die Unsicherheit der Spec fehlt, und es verrät Wissen.

- [ ] Je Spieler eine Erinnerung an gesehene fremde Gebäude (Typ, Lage, Bauphase, Zeitalter,
      Besitzer), gezeichnet statt des echten Gebäudes, solange keine Kachel davon sichtbar ist
- [ ] Minimap und Auswahl folgen derselben Erinnerung; Spielstand speichert sie

### F8 · Markt: Rohstoffe tauschen [R]

Stand: Markt baubar (Feudalzeit), ohne Funktion. Spec: Tausch zu schlechtem Kurs, der sich
je Handel in dieselbe Richtung verschlechtert - die Notlösung, wenn Gold oder Stein ausgeht.

- [ ] Kurse in AoE.Core (Kauf/Verkauf je 100 gegen Gold, Kursänderung je Handel, langsame
      Erholung) mit Tests
- [ ] Tasten am ausgewählten Markt, Kurse in der Leiste; Spielstand speichert die Kurse
- [ ] KI handelt, wenn ihr eine Ressource ausgeht
- Handelskarren brauchen einen verbündeten Markt - erst mit Teams (F19)

### F9 · Wunder-Sieg und Aufgeben [R]

Stand: einzige Siegbedingung ist ein zerstörtes Stadtzentrum, für das kein neues mehr baubar
ist (`DefeatRule`). Das Wunder ist ab der Imperialzeit baubar, hat aber keine Wirkung.

- [ ] Fertiges Wunder startet eine für beide sichtbare Uhr; steht es bis zum Ablauf, ist das
      Spiel gewonnen; die KI greift ein gegnerisches Wunder vorrangig an
- [ ] „Aufgeben" im Pausenmenü
- [ ] Entscheidung: bleibt „Stadtzentrum weg = verloren", oder wie in der Spec erst, wenn
      keine Gebäude und Einheiten mehr stehen

### F10 · Garnison [X]

Stand: es gibt keine Garnison. Spec: Einheiten ziehen sich in Gebäude zurück, Türme und Burgen
schießen mit mehr Pfeilen, je mehr drin sind; das Stadtzentrum schützt Dorfbewohner.

- [ ] Befehl „betreten" für Stadtzentrum, Wachturm und Burg (Plätze je Gebäude), Taste zum Entlassen
- [ ] Mehr Pfeile je Insasse (setzt F1 voraus), Insassen heilen langsam; Spielstand speichert sie
- [ ] Alarm: alle Dorfbewohner in der Nähe gehen ins Stadtzentrum

### F11 · Jagd wie in AoE: Hirsch flieht, Wildschwein wehrt sich, Fleisch verdirbt [R]

Stand: Rehe und Wildschweine wandern, ein Dorfbewohner schlachtet sie ohne Gegenwehr, das
Fleisch hält ewig. Bären und Wölfe greifen schon an (RT).

- [ ] Reh flieht vor Dorfbewohnern und muss erst erlegt werden
- [ ] Wildschwein greift den Jäger an (Raubtier-Logik aus RT) und lässt sich zum Stadtzentrum locken
- [ ] Erlegtes Wild verliert langsam Nahrung (Spec: die Sammelmannschaft muss groß genug sein)

### F12 · Haltungen, Formationen, gemeinsames Tempo [R]

Stand: Gruppen verteilen sich ums Ziel (`FormationMovement`, Verbands-Abstand in den
Einstellungen), untätige Soldaten greifen sichtbare Angreifer in der Nähe von selbst an.

- [ ] Haltungen aggressiv / defensiv / Stellung halten / kein Angriff, Taste in der Leiste
- [ ] Ein Verband läuft so schnell wie seine langsamste Einheit (der Späher läuft heute voraus)
- [ ] Formationen Linie, Kasten, gestaffelt, Flanke: Nahkämpfer vorn, Fernkämpfer dahinter

### F13 · HUD-Mitte: Angriff, Rüstung, Bild der Auswahl [R]

Stand: für eine einzelne Einheit stehen Name und Lebenspunkte als Text in der Leiste. Spec:
Name, Porträt, Lebensbalken, Angriff/Rüstung, bei Gebäuden die Warteschlange.

- [ ] Porträt (das Standbild der Figur), Lebensbalken, Angriff und Rüstung (Nah/Fern) mit Boni
- [ ] Bei Gruppen: kleine Bilder je Einheit statt nur der Anzahl

### F14 · Elite-Upgrades [R]

Miliz → Schwertkämpfer → Langschwertkämpfer, Bogenschütze → Armbrustschütze, Späher → Leichte
Kavallerie; Forschung im Ausbildungsgebäude, hebt Werte und tauscht Bild und Namen aller schon
vorhandenen Einheiten. Bilder je Stufe (Qwen). Weitere Linien nach F2.

### F15 · Belagerung: Rammbock, Mangonel, Trebuchet [X]

Stand: die Belagerungswerkstatt (Ritterzeit) ist baubar und bildet nichts aus.

- [ ] Rammbock: +125 gegen Gebäude, gegen Einheiten fast nichts, hohe Fernkampf-Rüstung
- [ ] Mangonel: Flächenschaden mit Mindestreichweite; Trebuchet (Imperialzeit, auch Burg):
      auf- und abbauen, Reichweite 16, nur gegen Gebäude
- [ ] Mehrere Bevölkerungsplätze, Reparatur durch Dorfbewohner, Bilder und Geschosse

### F16 · Kloster: Mönche, Bekehrung, Reliquien [X]

Spec zählt das zu „bewusst nicht nötig fürs Grundgerüst" - das Kloster ist aber schon baubar
und tut nichts.

- [ ] Mönch heilt eigene Einheiten und bekehrt fremde (Zeit, Zufall, Reichweite; Gebäude und
      Belagerung immun); Forschung Glaube
- [ ] Reliquien auf der Karte; ein Mönch trägt sie ins Kloster, jede bringt laufend Gold;
      danach Reliquien-Sieg zu F9

### F17 · Burg mit eigener Einheit [X]

Stand: Burg baubar (650 Stein), nur Sicht. Ohne Zivilisationen eine feste Burg-Einheit
(`UnitType.Longbowman` gibt es schon), dazu Pfeile aus F1 und Trebuchet aus F15.

### F18 · Farm neu bestellen kostet Holz [R] - Entscheidung des Nutzers

Stand: eine leere Farm wächst nach 100 s kostenlos nach (`FARM_REGROW_SECONDS`). Spec: eine
Neubestellung kostet 60 Holz - das ist der Holzbedarf des späten Spiels. Optional die
vorbestellte Warteschlange in der Mühle wie in AoE II DE.

### F19 · Karte, Startaufstellung, Spielmodi [X]

- [ ] Startaufstellung wie AoE II: 3 Dorfbewohner und 1 Späher (heute 4 Dorfbewohner, kein Späher)
- [ ] Gelände: Schnee und Fels stehen in `TileType`, werden aber nie erzeugt; Untiefen
      (begehbares Flachwasser) fehlen; Erhöhungen mit Angriffs- und Sichtvorteil fehlen
- [ ] Kartentypen im Menü neben der Größe: offen (Arabia), Schwarzwald, Nomade; Inseln erst mit Schiffen
- [ ] Spielmodi Todesmatch und Königsmord; mehr als zwei Spieler, Teams, Rohstoffe an Verbündete

### Bewusste Abweichungen von der Spec - nur auf ausdrücklichen Wunsch ändern

- **Isometrische Darstellung** (Spec, Relevanz Punkt 7): das Spiel bleibt in Draufsicht mit
  Figuren in Seitenansicht (Nutzer 2026-10-05, keine Front-/Rückansichten). Ein Umstieg auf
  Rauten berührt Karte, Shader, Wege, Schatten und jedes Gebäudebild.
- **Maus:** Spec Rechtsklick = Befehl; hier Linksklick = Befehl, Rechtsklick = Auswahl (C8b).
- **Zivilisationen, Seekampf (Hafen, Schiffe), Kampagnen:** laut Spec kein Teil des Grundgerüsts.
- **Bären und Wölfe** (RT) gehen über die Spec hinaus.

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
- [x] 394 Unit-Tests grün (2026-10-16)
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
- **Grafik-Engpass der Spec ist überholt** (Stand 2026-10-23): Gebäude je Zeitalter, Figuren,
  Tiere, Bäume, Boden, Wege und Symbole kommen aus Qwen-Image (`tools/bilder`). Neue Einheiten
  aus Block F brauchen je Stand-, Lauf- und Schlagbilder - das ist dort der eigentliche Aufwand.
- **Bewusst nicht nötig** fürs Grundgerüst: Zivilisationsboni, Reliquien, Mönche, Handel,
  Seekampf, Formationen, Kampagnen.
