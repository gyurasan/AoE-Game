# Der Computergegner in Age of Empires

*Wie die eingebaute KI eine Partie spielt, von der ersten Sekunde bis zur Aufgabe*

> **Zur Quellenlage:** Der Aufbau der KI (Regelskripte, Strategiezahlen, Angriffsgruppen)
> ist in der Modding-Szene gut dokumentiert und hier so beschrieben, wie er funktioniert.
> Die Angaben dazu, **auf welcher Stufe die KI schummelt**, stammen dagegen überwiegend aus
> Spielerforen und sind entsprechend vorsichtig formuliert. Bezugspunkt ist wie in
> [AgeOfEmpires.md](AgeOfEmpires.md) vor allem **Age of Empires II** (AoK/AoC, HD, DE).

---

## Inhalt

1. [Kurzfassung](#kurzfassung)
2. [Wie die KI gebaut ist](#wie-die-ki-gebaut-ist)
3. [Die Stellschrauben: Strategiezahlen](#die-stellschrauben-strategiezahlen)
4. [Ablauf einer Partie aus Sicht der KI](#ablauf-einer-partie-aus-sicht-der-ki)
5. [Angriff: Gruppen, Wellen, Zielwahl](#angriff-gruppen-wellen-zielwahl)
6. [Verteidigung](#verteidigung)
7. [Strategie-Persönlichkeiten](#strategie-persönlichkeiten)
8. [Schwierigkeitsgrade und Schummeln](#schwierigkeitsgrade-und-schummeln)
9. [Typische Schwächen und wie Spieler sie ausnutzen](#typische-schwächen-und-wie-spieler-sie-ausnutzen)
10. [Abgleich mit unserer KI](#abgleich-mit-unserer-ki)
11. [Quellen](#quellen)

---

## Kurzfassung

Der Computergegner in Age of Empires **denkt nicht voraus**. Er ist ein Regelwerk, das in
kurzen Abständen immer wieder dieselbe Liste von Wenn-dann-Regeln abarbeitet:
„Wenn ich Nahrung habe und das Stadtzentrum frei ist → Dorfbewohner ausbilden",
„Wenn das Haus-Polster unter 5 fällt → Haus bauen", „Wenn ich 12 Soldaten habe und
Minute 18 erreicht ist → angreifen". Das Kleinklein — Wegfindung, welcher Baum gefällt
wird, welches Ziel ein Schwertkämpfer gerade schlägt — erledigt die Spiel-Engine selbst.

Daraus ergibt sich ein sehr typisches Verhaltensmuster:

1. **Wirtschaft zuerst und ohne Pause** — das Stadtzentrum steht nie still, Häuser kommen
   rechtzeitig, Lager entstehen direkt an den Rohstoffen.
2. **Feste Aufteilung der Dorfbewohner** auf Nahrung, Holz, Gold und Stein, je nach
   Zeitalter umgestellt.
3. **Aufstieg** in das nächste Zeitalter bei einer festen Zahl von Dorfbewohnern.
4. **Armee sammeln** bis zu einer Mindestgröße, dann **Angriff in Wellen**.
5. **Bei Angriff aufs eigene Dorf** wird die Armee zurückgezogen, Dorfbewohner flüchten
   oder wehren sich.
6. **Verluste ersetzen**, nächste Welle — bis einer aufgibt.

Je höher die Schwierigkeit, desto schneller, dichter und härter läuft diese Schleife ab.
Das Prinzip bleibt gleich.

---

## Wie die KI gebaut ist

### Age of Empires I (1997)

Jeder Computerspieler besteht aus drei Textdateien im Ordner `AI`:

| Datei | Inhalt |
|---|---|
| `.ai` | **Bauliste** — Reihenfolge, in der Gebäude, Einheiten und Technologien entstehen |
| `.cty` | **Stadtplan** — wo die Gebäude relativ zum Stadtzentrum stehen |
| `.per` | **Persönlichkeit** — Stellschrauben wie Angriffslust, Gruppengröße, Erkundung |

Die KI arbeitet die Bauliste im Wesentlichen von oben nach unten ab. Das macht sie
berechenbar: Sie spielt jede Partie fast identisch, egal was der Gegner tut.

### Age of Empires II (1999 bis heute)

Die Bauliste fällt weg, an ihre Stelle tritt ein **Regelskript** (`.per`, Lisp-artige
Syntax). Jede Regel hat Bedingungen links und Aktionen rechts vom Pfeil:

```lisp
; Häuser rechtzeitig bauen, bevor die Bevölkerungsgrenze erreicht ist
(defrule
    (housing-headroom < 5)
    (population-headroom > 0)
    (can-build house)
=>
    (build house))

; Dorfbewohner ausbilden, solange es geht
(defrule
    (can-train villager)
=>
    (train villager))

; Nach 18 Minuten mit genug Soldaten einmal angreifen
(defrule
    (game-time > 1100)
    (soldier-count > 10)
=>
    (set-strategic-number sn-minimum-attack-group-size 6)
    (set-strategic-number sn-percent-attack-soldiers 80)
    (attack-now)
    (disable-self))
```

Dazu kommen:

- **Fakten** (`game-time`, `food-amount`, `town-under-attack`, `enemy-buildings-in-town`,
  `players-military-population` …) — das, was die KI über die Welt abfragen kann.
- **Ziele** (`goal`) und **Timer** — kleine Speicherzellen, mit denen ein Skript sich merkt,
  in welcher Phase es ist („Rush läuft", „Angriff vor 90 s gestartet").
- **Strategiezahlen** (`sn-…`) — Stellschrauben, die das Engine-Verhalten steuern
  (nächster Abschnitt).

Die Engine wertet alle Regeln in kurzen Abständen erneut aus. Eine Regel ohne
`disable-self` feuert also immer wieder, sobald ihre Bedingungen stimmen — so entsteht
das „Stadtzentrum steht nie still"-Verhalten fast von selbst.

**Arbeitsteilung:** Das Skript sagt nur *was* passieren soll („baue ein Holzfällerlager",
„greife an"). *Wie* — Bauplatz, Weg, welcher Dorfbewohner, welches Ziel — entscheidet die
Engine. Die Skriptsprache wurde in den Neuauflagen stark erweitert (DE erlaubt einzelne
Einheiten direkt zu steuern), das Grundprinzip ist aber gleich geblieben.

### Spätere Teile

- **Age of Empires III** nutzt statt Regeldateien echte Skripte in **XS** (C-ähnlich),
  mit Plänen für Bauen, Sammeln, Erkunden und Angreifen.
- **Age of Empires II DE** und **IV** haben die mit Abstand stärksten KIs der Reihe; die
  Standard-KI von AoE II DE geht auf die Community-KI „Barbarian" zurück.

---

## Die Stellschrauben: Strategiezahlen

Ein Großteil des Verhaltens wird nicht über Regeln, sondern über Zahlen gesteuert, die
das Skript laufend anpasst. Die wichtigsten (AoE II):

| Strategiezahl | Wirkung |
|---|---|
| `sn-food-gatherer-percentage` … `sn-stone-gatherer-percentage` | Anteil der Dorfbewohner pro Rohstoff (z. B. 60/40/0/0 am Anfang, später 40/30/25/5) |
| `sn-percent-civilian-explorers`, `sn-total-number-explorers` | Wer und wie viele erkunden |
| `sn-minimum-attack-group-size` / `sn-maximum-attack-group-size` | Ab wie vielen Einheiten eine Angriffsgruppe losläuft, wie groß sie höchstens wird |
| `sn-percent-attack-soldiers` | Wie viel Prozent der Armee angreifen (der Rest bleibt daheim) |
| `sn-number-attack-groups`, `sn-number-defend-groups` | Anzahl paralleler Angriffs- und Verteidigungsgruppen |
| `sn-minimum-town-size` / `sn-maximum-town-size` | Radius des eigenen „Stadtgebiets" — was darin auftaucht, gilt als Angriff |
| `sn-target-evaluation-distance`, `…-hitpoints`, `…-attack-attempts`, `…-randomness` | Gewichte für die Zielwahl beim Angriff |

Eine Strategie ist damit im Kern eine **Kurve dieser Zahlen über die Zeit**: früh viel
Nahrung und keine Angreifer, ab der Feudalzeit Gold dazu und kleine Angriffsgruppen, in der
Ritterzeit große Gruppen mit hohem Angriffsanteil.

---

## Ablauf einer Partie aus Sicht der KI

### Start (Minute 0–2)

- Stadtzentrum bildet **sofort und ununterbrochen** Dorfbewohner aus.
- Der **Späher** erkundet in Spiralen um die eigene Basis: Schafe, Wildschweine, Beeren,
  Wald, Gold und Stein werden gesucht. Erst danach zieht er Richtung Gegner.
- Die ersten Dorfbewohner gehen auf Schafe am Stadtzentrum; zwei bauen Häuser.

### Dunkle Zeit (Wirtschaftsaufbau)

- **Lager kommen an die Rohstoffe**, nicht ans Stadtzentrum: Holzfällerlager an den
  nächsten großen Wald, Mühle an die Beeren, Bergbaulager an Gold und Stein.
- **Häuser** werden gebaut, sobald das Polster zur Bevölkerungsgrenze unter ~5 fällt —
  die KI läuft deshalb kaum je in den „Pop-Lock".
- Wenn Schafe, Wild und Beeren zur Neige gehen, entstehen **Farmen** rund um Stadtzentrum
  und Mühle.
- Neue Dorfbewohner gehen dorthin, wo die Ist-Verteilung am weitesten unter der
  Soll-Verteilung liegt.

### Aufstieg

Die KI steigt auf, sobald eine feste Zahl Dorfbewohner erreicht ist (typisch 20–30 je nach
Strategie und Stufe) **und** die Kosten da sind. Kurz vorher wird nur noch für den Aufstieg
gesammelt. Gleichzeitig mit dem Aufstieg verschiebt das Skript die Strategiezahlen auf
den nächsten Abschnitt (mehr Gold, erste Militärgebäude).

### Feudalzeit (erster Druck)

- **Militärgebäude** entstehen — je nach Persönlichkeit Kaserne, Schießstand oder Stall.
- Wirtschafts-Upgrades (doppelschneidige Axt, Pferdekummet …) in der Schmiede-, Lager- und
  Mühlenreihe werden „mitgenommen", sobald Rohstoffe übrig sind.
- Aggressive KIs schicken jetzt die ersten kleinen Gruppen los (siehe
  [Strategie-Persönlichkeiten](#strategie-persönlichkeiten)).

### Ritterzeit (Hauptphase)

- **Zusätzliche Stadtzentren** (Boom), Burg, Universität, Belagerungswerkstatt.
- Armee wird auf eine **Zusammensetzung** hingebaut, die zur Zivilisation passt
  (Franken → Ritter, Briten → Langbogen …). Die neueren KIs **kontern** zusätzlich, was sie
  beim Gegner gesehen haben: viele Bogenschützen → Plänkler und Rammböcke; viele Ritter →
  Pikeniere und Kamele.
- Angriffe werden größer und kommen in **regelmäßigen Wellen**.

### Imperialzeit (Endspiel)

- Elite-Upgrades, Trebuchets gegen Burgen und Türme.
- Wenn **Gold knapp** wird, schwenkt die KI auf goldfreie Einheiten (Pikeniere, Plänkler,
  leichte Kavallerie) und verlagert Dorfbewohner auf Holz und Nahrung.
- Auf Wasserkarten läuft parallel ein eigener Seestrang (Fischerboote, Galeeren,
  Brander).

### Ende

Die KI **gibt auf**, wenn die Lage aussichtslos ist (kein Stadtzentrum, kaum noch
Dorfbewohner, keine Armee). Ansonsten wird sie bis zur Vernichtung des letzten Gebäudes
gespielt. Auf niedrigen Stufen kündigt sie die Aufgabe oft per Chatnachricht an.

---

## Angriff: Gruppen, Wellen, Zielwahl

So läuft ein Angriff der klassischen AoE-II-KI ab:

1. **Sammeln.** Neue Soldaten laufen zu einem Sammelpunkt im eigenen Dorf.
2. **Losschlagen.** Sobald `sn-minimum-attack-group-size` erreicht ist und eine Regel
   `attack-now` auslöst (oder die Engine von selbst, wenn Angriffsgruppen erlaubt sind),
   zieht der eingestellte Prozentsatz der Armee los. Ein Rest bleibt als Verteidigung.
3. **Ziel wählen.** Die Engine bewertet gegnerische Ziele nach Entfernung, Trefferpunkten,
   bisherigen Angriffsversuchen und etwas Zufall. Bevorzugt werden **Gebäude in Reichweite**
   und **Dorfbewohner**; Einheiten, die auf dem Weg angreifen, werden bekämpft.
4. **Durchziehen.** Die klassische KI zieht sich praktisch **nicht zurück**: Die Gruppe
   kämpft, bis sie aufgerieben ist. Die DE-KI bricht Angriffe ab, wenn sie klar unterlegen
   ist, und holt Belagerungswaffen nach.
5. **Nächste Welle.** Nachschub sammelt sich neu, der nächste Angriff folgt nach einer
   festen Pause oder sobald die Mindestgröße wieder erreicht ist.

**Zielspieler:** Bei mehreren Gegnern greift die KI meist den **nächstgelegenen** an, oder
den, der sie zuletzt angegriffen hat. Verbündete Menschen können sie per Spott-Nummer
(„Taunt", z. B. „31 – Attack an enemy now") zu einem Angriff auffordern.

---

## Verteidigung

- **Stadtgebiet:** Alles Feindliche innerhalb der Stadtgröße (`sn-…-town-size`) löst den
  Fakt `town-under-attack` aus.
- **Verteidigungsgruppen** werden zusammengezogen; laufende Angriffsgruppen können
  zurückbeordert werden.
- **Dorfbewohner** reagieren je nach Skript unterschiedlich: Sie **flüchten ins
  Stadtzentrum / in Türme** (Garnison), oder sie **wehren sich selbst** gegen kleine
  Störtrupps (klassisch gegen einen frühen Späher- oder Milizrush).
- **Befestigung:** Wer früh angegriffen wird, baut Palisaden und Türme; die DE-KI mauert
  sich auf manchen Karten auch vorsorglich ein.
- **Wiederaufbau:** Zerstörte Lager, Häuser und Farmen werden automatisch neu gesetzt —
  die Regeln „wenn weniger als N von X, baue X" feuern einfach wieder.

---

## Strategie-Persönlichkeiten

Eine KI wählt zu Beginn einer Partie eine Strategie aus (abhängig von Zivilisation, Karte
und Zufall) und spielt sie weitgehend durch:

| Strategie | Verhalten der KI | Erkennungszeichen |
|---|---|---|
| **Rush** | Kaserne/Stall schon in der Dunklen Zeit oder früh in der Feudalzeit, kleine Trupps (Miliz, Späher, Bogenschützen) stören Holzfäller und Goldsammler | Erste Feinde bei Minute 8–12 |
| **Drush / Flush** (DE) | Miliz-Rush in der Dunklen Zeit, direkt gefolgt von Bogenschützen in der Feudalzeit | Zwei Angriffswellen kurz hintereinander |
| **Fast Castle** | Militär weitgehend überspringen, schnell in die Ritterzeit, dann Ritter oder Burg | Lange Ruhe, dann plötzlich starke Armee |
| **Boom** | Mehrere Stadtzentren, sehr viele Dorfbewohner, spät, aber massiv | Riesiges Dorf, Armee erst ab Ritterzeit |
| **Turtle** | Mauern, Türme, Burgen, Belagerung erst spät | Steinmauer um die Basis |

---

## Schwierigkeitsgrade und Schummeln

### Was die Stufe verändert

Die Stufen (in AoE II DE: *Easiest, Standard, Moderate, Hard, Hardest, Extreme*) laufen
im Wesentlichen **dasselbe Skript mit anderen Zahlen**:

| Niedrige Stufe | Hohe Stufe |
|---|---|
| Ausbildung mit Pausen, wenige Dorfbewohner | Stadtzentrum ohne Leerlauf, 100+ Dorfbewohner |
| Späte, kleine Angriffe; auf der niedrigsten Stufe oft gar keiner | Frühe Angriffe, große Wellen |
| Kaum Upgrades, steigt spät oder gar nicht auf | Alle relevanten Upgrades, zügiger Aufstieg |
| Keine Konter, feste Einheitenwahl | Kontert die gesehene Armee |
| Kein Mikromanagement | Zieht verwundete Einheiten zurück, weicht Katapulten aus (DE) |

### Schummelt die KI?

- **AoE I:** Nach Spielerberichten erhält die KI auf *Hardest* einen Rohstoffbonus; in der
  Definitive Edition angeblich auch auf *Hard*. Mods, die diesen Bonus abschalten, gibt es.
- **AoE II (Original/HD):** Die höheren Stufen der HD-KI erhielten laut Spielerberichten
  zusätzliche Rohstoffe.
- **AoE II DE:** In freien Partien gilt die *Extreme*-KI in der Community als
  **schummelfrei** — sie ist stärker, weil sie besser spielt, nicht weil sie mehr hat.
  Eine offizielle Bestätigung dafür ließ sich nicht finden.
- **Kleinere Vorteile** bleiben auf allen Stufen: Die KI kann über Fakten Dinge abfragen,
  die ein Mensch nur schätzen kann (z. B. die Militärbevölkerung des Gegners), und klickt
  nie daneben.
- **Kampagnen und Szenarien** sind ein Sonderfall: Dort erhält die KI häufig
  Rohstofflieferungen per Auslöser, damit Missionen funktionieren.

---

## Typische Schwächen und wie Spieler sie ausnutzen

Vor allem die klassische KI (AoE I, AoE II vor DE) hat Muster, die erfahrene Spieler
gezielt ausnutzen:

- **Mauern verwirren sie.** Die KI läuft weite Umwege, greift die Mauer statt der Einheiten
  dahinter an oder findet gar keinen Weg und bleibt stehen.
- **Türme und Burgen als Falle.** Angriffsgruppen laufen ohne Belagerungswaffen in den
  Beschuss und gehen dabei unter.
- **Angriffe tröpfchenweise.** Nachschub läuft einzeln zur Front statt zu sammeln — leichte
  Beute für eine stehende Verteidigung.
- **Ablenkung.** Eine einzelne schnelle Einheit im Dorf zieht oft die ganze Armee von ihrem
  eigentlichen Ziel weg.
- **Kein Rückzug.** Verlorene Kämpfe werden bis zur letzten Einheit ausgefochten.
- **Vorhersehbarkeit.** Gleicher Zeitpunkt, gleicher Weg, gleiches Ziel — wer einmal gegen
  eine Persönlichkeit gespielt hat, kennt die nächste Partie.
- **Schwache Reaktion auf Störungen.** Wird die Holzwirtschaft früh gestört, bricht die
  Bauliste ins Stocken, weil die Regeln auf Rohstoffe warten, die nicht mehr kommen.

Die DE-KI hat die meisten dieser Schwächen abgestellt: Sie mauert selbst, zieht sich
zurück, verteilt Bogenschützen gegen Flächenschaden und reagiert auf frühen Druck.

---

## Abgleich mit unserer KI

Stand: `src/AoE.Core/Ai/EconomyAi.cs`, Oktober 2026. Unsere KI folgt demselben Prinzip wie
das Original — ein deterministisches Regelwerk, das in jedem `Tick` eine feste Liste
abarbeitet; die Engine (`IWorldActions`) führt Wege und Sammeln aus.

| Verhalten im Original | Bei uns | Stand |
|---|---|---|
| Stadtzentrum bildet ohne Pause aus | `TrainIfPossible`, Warteschlange bis 12 | vorhanden |
| Häuser vor der Pop-Grenze | Haus erst *am* Limit, und nur solange noch **gar kein** Haus steht (`!Buildings.Any(House)`) | **lückenhaft** — die KI baut höchstens ein Haus |
| Lager an den Rohstoffen | `PlanAnchored`, Ankerradius 20 Kacheln; Bergbaulager nur an Stein | vorhanden, Gold ohne eigenes Lager |
| Farmen, wenn Nahrung knapp | Farm unter 200 Nahrung, ebenfalls nur solange keine Farm steht | **lückenhaft** — höchstens eine Farm |
| Erkundung mit Späher | ein Dorfbewohner als fester Erkunder Richtung Kartenmitte | vorhanden, ohne Spirale um die eigene Basis |
| Soll-Verteilung der Dorfbewohner | feste Reihenfolge Nahrung → Holz → Stein → Gold | **fehlt** — kein Prozentmodell |
| Aufstieg bei fester Dorfbewohnerzahl | Aufstieg, sobald die Kosten da sind | vereinfacht |
| Angriff ab Mindestgruppe in Wellen | Angriff mit 1+ Soldat auf den nächsten sichtbaren Feind, 8 s Pause | **stark vereinfacht** — keine Sammelphase |
| Rest der Armee bleibt daheim | 1 Soldat Heimwache | vorhanden |
| Zielbewertung (Entfernung, TP, Typ) | nächstgelegene Einheit, sonst nächstes Gebäude | vereinfacht |
| Dorfbewohner wehren sich | bis 4 untätige Dorfbewohner im Radius 8 | vorhanden |
| Garnison / Flucht ins Stadtzentrum | — | fehlt |
| Konter auf gesehene Einheiten | feste Wahl Miliz, sonst Bogenschütze | fehlt |
| Strategie-Persönlichkeiten | eine feste Strategie | fehlt |
| Schwierigkeitsgrade | — | fehlt |
| Rückzug bei Unterlegenheit | — | fehlt |
| Aufgabe bei aussichtsloser Lage | Niederlage beendet das Spiel | vorhanden (Niederlage, keine aktive Aufgabe) |

### Naheliegende nächste Schritte

Nach Wirkung auf das Spielgefühl geordnet:

1. **Mehr als ein Haus und eine Farm** — die Sperre nur auf *unfertige* Gebäude beziehen
   und das Haus schon mit Polster (Restplatz < 3–5) bauen statt erst am Limit. Sonst
   bleibt die KI bei der Bevölkerung von Stadtzentrum plus einem Haus stehen.
2. **Sammeln vor dem Angriff** — eine Mindestgruppengröße (z. B. 5) und ein Sammelpunkt.
   Das allein macht aus dem jetzigen „Einzeln-hinrennen" eine erkennbare Angriffswelle.
3. **Soll-Verteilung der Dorfbewohner** als Prozentwerte je Zeitalter statt fester
   Reihenfolge — sonst sammelt die KI Gold und Stein erst, wenn keine Nahrung oder kein
   Holz in Reichweite ist.
4. **Schwierigkeitsgrade** als Satz von Zahlen (Gruppengröße, Angriffspause,
   Soldatenziel, Ausbildungstempo) — genau wie im Original kein zweites Regelwerk.
5. **Rückzug** bei klarer Unterlegenheit, damit Angriffe nicht im Turmfeuer verbrennen.

---

## Quellen

Die Angaben zu Schwierigkeitsgraden und Schummeln beruhen auf Spielerberichten aus
diesen Diskussionen:

- [How can I beat the moderate a.i. difficulty when it cheats so much? (AoE-Forum)](https://forums.ageofempires.com/t/how-can-i-beat-the-moderate-a-i-difficulty-when-it-cheats-so-much/278076)
- [AI difficulty levels (AoE-Forum)](https://forums.ageofempires.com/t/ai-difficulty-levels/107341)
- [AoE II DE – Diskussion zur KI-Schwierigkeit (Steam)](https://steamcommunity.com/app/813780/discussions/0/3186864655193380830)
- [Some Insights on AI in AoE: DE (AoE-Forum)](https://forums.ageofempires.com/t/some-insights-on-ai-in-aoe-de-and-a-couple-of-suggested-features/32801)
- [How to get the best out of the AI in custom game (AoE-Forum)](https://forums.ageofempires.com/t/how-to-get-the-best-out-of-the-ai-in-custom-game/32657)
- [Three ways to get the AI to attack (AoE-Forum)](https://forums.ageofempires.com/t/three-ways-to-get-the-ai-to-attack/205476/4)
- [Creating simple AI scripts for your custom campaigns (AoE-Forum)](https://forums.ageofempires.com/t/creating-simple-ai-scripts-for-your-custom-campaigns/210881)

---

*Erstellt am 10.10.2026*
