# Age of Empires

*Echtzeit-Strategiespiel-Reihe (RTS) mit historischem Setting*

> **Zu den Zahlen:** Alle konkreten Werte in diesem Dokument stammen aus **Age of Empires II
> (The Age of Kings / Definitive Edition)** und sind als **Größenordnung** gedacht — sie zeigen
> die Verhältnisse, auf denen das Balancing beruht. Wenn du sie 1:1 in Code übernehmen willst,
> prüfe sie vorher gegen eine Referenz (offizielles Wiki / Spieldateien). Die *Mechaniken* und
> *Formeln* dagegen sind exakt so beschrieben, wie sie funktionieren.

---

## Inhalt

1. [Kurzbeschreibung](#kurzbeschreibung)
2. [Die Teile der Reihe](#die-teile-der-reihe)
3. [Die vier Ressourcen im Detail](#die-vier-ressourcen-im-detail)
4. [Der Dorfbewohner-Loop](#der-dorfbewohner-loop)
5. [Die Zeitalter](#die-zeitalter)
6. [Bevölkerung und Häuser](#bevölkerung-und-häuser)
7. [Gebäude](#gebäude)
8. [Einheiten und das Konter-System](#einheiten-und-das-konter-system)
9. [Die Schadensformel](#die-schadensformel)
10. [Technologien](#technologien)
11. [Mönche und Bekehrung](#mönche-und-bekehrung)
12. [Sichtbarkeit und Nebel des Krieges](#sichtbarkeit-und-nebel-des-krieges)
13. [Bewegung, Wegfindung, Formationen](#bewegung-wegfindung-formationen)
14. [Die Karte](#die-karte)
15. [Ablauf einer Partie](#ablauf-einer-partie)
16. [Siegbedingungen und Spielmodi](#siegbedingungen-und-spielmodi)
17. [Zivilisationen](#zivilisationen)
18. [Die Benutzeroberfläche](#die-benutzeroberfläche)
19. [Isometrische Darstellung](#isometrische-darstellung)
20. [Warum die Reihe funktioniert](#warum-die-reihe-funktioniert)
21. [Relevanz für dieses Projekt](#relevanz-für-dieses-projekt)

---

## Kurzbeschreibung

**Age of Empires** ist eine der einflussreichsten Echtzeit-Strategie-Reihen überhaupt.
Der Spieler startet mit einer Handvoll Dorfbewohner und einem Stadtzentrum und baut daraus
über mehrere Zeitalter hinweg eine Zivilisation auf: Ressourcen sammeln, Siedlung
errichten, Technologien erforschen, Armee ausheben, Gegner besiegen.

Das Besondere gegenüber anderen RTS-Spielen: Der Fortschritt ist an **historische Epochen**
gebunden. Man spielt reale Völker (Briten, Franken, Mongolen, Azteken …) und bewegt sich
von der Steinzeit bzw. dem Frühmittelalter bis in die Neuzeit. Wirtschaftsaufbau und
Militär sind dabei gleichwertig — wer nur kämpft, verliert; wer nur baut, auch.

- **Genre:** Echtzeit-Strategie, 4X-nah
- **Perspektive:** isometrisch bzw. schräge Draufsicht (2D-Sprites bis AoE III, 3D ab AoE IV)
- **Entwickler:** Ensemble Studios (I–III), Relic Entertainment / World's Edge (IV)
- **Publisher:** Microsoft
- **Erstveröffentlichung:** 1997

---

## Die Teile der Reihe

| Titel | Jahr | Epoche | Anmerkung |
|---|---|---|---|
| Age of Empires | 1997 | Steinzeit → Eisenzeit | Der Ursprung, Antike |
| Age of Empires II: The Age of Kings | 1999 | Mittelalter | **Der prägendste Teil**, bis heute kompetitiv gespielt |
| Age of Mythology | 2002 | Mythologie | Spin-off mit Göttern und Fabelwesen |
| Age of Empires III | 2005 | Kolonialzeit | Heimatstadt-System, Karten statt reiner Wirtschaft |
| Age of Empires: Online | 2011 | Antike | F2P-MMO-Ableger, eingestellt |
| Age of Empires IV | 2021 | Mittelalter | Rückkehr zu den Wurzeln, moderne 3D-Engine |

Dazu die **Definitive Editions** (AoE I 2018, AoE II 2019, AoE III 2020) — Remaster mit
4K-Grafik, überarbeiteter KI, neuen Zivilisationen und aktivem Online-Multiplayer.
**Age of Empires II: Definitive Edition** ist bis heute der meistgespielte Teil.

---

## Die vier Ressourcen im Detail

Alles im Spiel kostet eine Kombination aus vier Ressourcen. Entscheidend ist nicht nur
*wie viel* etwas kostet, sondern *welche* Ressource — jede hat ein eigenes Angebotsprofil.

### Übersicht

| Ressource | Endlich? | Frühe Quelle | Späte Quelle | Hauptverwendung |
|---|---|---|---|---|
| **Nahrung** | nein (Farmen) | Schafe, Beeren, Wild | Farmen, Fischerboote | Dorfbewohner, Militär, Zeitalter |
| **Holz** | praktisch nein | Wald | Wald | Gebäude, Farmen, Bogenschützen, Schiffe |
| **Gold** | **ja** | Goldminen | Handel, Reliquien | Elite-Einheiten, Technologien |
| **Stein** | **ja** | Steinbrüche | — | Mauern, Türme, Burgen, Stadtzentren |

Der wichtigste Punkt: **Gold und Stein sind endlich.** Farmen liefern unbegrenzt Nahrung,
Wald hält faktisch ewig — aber Minen sind irgendwann leer. Genau daraus entsteht der
Zeitdruck, der lange Partien entscheidet, und der Grund, warum Kartenkontrolle wichtiger
ist als eine große Armee.

### Nahrungsquellen im Einzelnen

| Quelle | Menge | Besonderheit |
|---|---|---|
| **Schafe** | ~100 | Direkt am Stadtzentrum, kein Lauf­weg. Verdirbt, wenn zu wenige Sammler dran sind. |
| **Beerenbüsche** | ~125 | Braucht eine Mühle in der Nähe als Abgabestelle. |
| **Wildschwein** | ~340 | Viel Nahrung, muss aber zum Stadtzentrum *gelockt* werden (es greift an). |
| **Hirsch** | ~140 | Flieht beim Angriff — muss erlegt und dann vor Ort gesammelt werden. |
| **Farm** | 175 (Basis) | Unbegrenzt nachbaubar, kostet 60 Holz pro Neubesetzung. |
| **Fisch (Küste)** | ~200 | Von Land aus abbaubar, sehr effizient. |

Jagdbare Tiere **verderben** über die Zeit, sobald sie tot sind — deswegen muss die
Sammelmannschaft groß genug sein, um sie schnell abzubauen. Das ist eine der Feinheiten,
die den frühen Wirtschaftsaufbau anspruchsvoll macht.

### Sammelraten (Richtwerte, Ressource pro Sekunde)

Diese Zahlen sind der Kern des Wirtschafts-Balancings. Sie liegen alle **eng beieinander**
(rund 0,3–0,4 pro Sekunde) — die Unterschiede entstehen weniger durch die Rate als durch
**Laufwege** und **Ablenkung**.

| Tätigkeit | Rate | Anmerkung |
|---|---|---|
| Schafe | ~0,33 /s | Kein Laufweg → effektiv sehr hoch |
| Beeren | ~0,31 /s | Braucht Mühle |
| Wild/Wildschwein | ~0,41 /s | Höchste Rohrate, aber Aufwand beim Locken |
| Farm | ~0,37 /s | Konstant, aber Holzkosten |
| Holz | ~0,39 /s | Laufweg wächst, wenn der Wald abgeholzt wird |
| Gold | ~0,38 /s | Mining Camp daneben bauen |
| Stein | ~0,36 /s | Mining Camp daneben bauen |

**Tragekapazität:** ein Dorfbewohner trägt standardmäßig **10 Einheiten**, läuft dann zur
nächsten Abgabestelle und wieder zurück. Genau deshalb baut man **Lager direkt neben die
Ressource** — der Laufweg ist der eigentliche Kostenfaktor, nicht die Sammelrate.

**Abgabestellen:** Stadtzentrum (alles), Mühle (Nahrung), Holzfällerlager (Holz),
Bergbaulager (Gold + Stein).

### Handel und Markt

Der **Markt** erlaubt, Ressourcen gegeneinander zu tauschen — zu einem schlechten Kurs, der
sich außerdem **dynamisch verschlechtert**, je mehr man in dieselbe Richtung handelt.
Er ist die Notlösung, wenn eine Ressource ausgegangen ist. Handelskarren zu einem
verbündeten Markt erzeugen **Gold** und sind im späten Spiel oft die einzige Goldquelle.

---

## Der Dorfbewohner-Loop

Das ist das Herzstück des Spiels. Wenn ein Klon *einen* Mechanismus richtig umsetzen muss,
dann diesen:

```
   ┌─────────────────────────────────────────────────────────┐
   │                                                         │
   ▼                                                         │
[Ressource suchen] → [hinlaufen] → [sammeln bis Traglast 10] │
                                            │                │
                                            ▼                │
                        [zur nächsten Abgabestelle laufen] ──┘
                                            │
                                            ▼
                                  [Ressource gutschreiben]
```

Dazu gehören mehrere Details, die das Spielgefühl ausmachen:

- **Automatische Fortsetzung:** Nach der Abgabe geht der Dorfbewohner selbstständig zur
  Ressource zurück. Der Spieler klickt nur einmal.
- **Automatische Nachbesetzung:** Ist ein Baum gefällt oder eine Mine leer, sucht der
  Dorfbewohner selbstständig die nächste gleiche Ressource in der Nähe.
- **Nächste Abgabestelle gewinnt:** Es wird immer die kürzeste Strecke gewählt, nicht das
  Stadtzentrum.
- **Bauen ist dasselbe Verhalten:** Mehrere Dorfbewohner an einem Gebäude bauen schneller
  (aber mit abnehmendem Ertrag, nicht linear).
- **Leerlauf ist der große Fehler.** Ein Dorfbewohner, der nichts tut, ist verlorene
  Ressource. Fortgeschrittene Spieler prüfen ständig auf „idle villagers" — das Spiel hat
  dafür sogar eine eigene Taste.

**Dorfbewohner-Eckdaten:** 25 Nahrung, ~25 s Ausbildungszeit, 25 HP (mit der Technologie
*Leinenkleidung* 40 HP). Er kann sich notfalls wehren, ist aber praktisch wehrlos.

---

## Die Zeitalter

Der zentrale Fortschrittsmechanismus. Der Aufstieg wird im Stadtzentrum erforscht, kostet
Ressourcen **und Zeit**, in der nichts Neues verfügbar ist.

| Zeitalter | Kosten | Dauer | Was es freischaltet |
|---|---|---|---|
| **Dunkle Zeit** | (Start) | — | Stadtzentrum, Haus, Mühle, Lager, Kaserne |
| **Feudalzeit** | 500 Nahrung | ~130 s | Schießstand, Stall, Markt, Schmiede, Wachturm, Farmen-Upgrade |
| **Ritterzeit** | 800 Nahrung, 200 Gold | ~160 s | Burg, Universität, Kloster, Belagerungswerkstatt, weitere Stadtzentren |
| **Imperialzeit** | 1000 Nahrung, 800 Gold | ~190 s | Trebuchet, Elite-Upgrades, Wunder, stärkste Technologien |

Der Zeitpunkt des Aufstiegs ist die wichtigste strategische Entscheidung im Spiel:

- **Früh aufsteigen** = stärkere Einheiten, aber weniger Dorfbewohner → schwächere Wirtschaft
- **Spät aufsteigen** = größere Wirtschaft, aber verwundbar gegen bessere Einheiten

Es gibt kein „richtig" — es hängt davon ab, was der Gegner tut. Genau das macht die
Aufklärung mit dem Späher so wichtig.

---

## Bevölkerung und Häuser

- Jede Einheit belegt **1 Bevölkerungsplatz** (manche Belagerungswaffen mehr).
- Ein **Haus** (25 Holz) gibt **+5 Plätze**, ein **Stadtzentrum** ebenfalls +5.
- Das Limit liegt standardmäßig bei **200** (einstellbar 25–500).

Der entscheidende Designpunkt: **Wirtschaft und Armee teilen sich dieselbe Obergrenze.**
Wer 140 Dorfbewohner hat, kann nur 60 Soldaten haben. Im späten Spiel werden deshalb
gezielt Dorfbewohner „abgebaut", um Platz für Militär zu schaffen.

Ist die Bevölkerungsgrenze erreicht, **stoppt die Produktion komplett** — ein klassischer
Anfängerfehler, der ganze Partien kostet. Deshalb baut man Häuser immer *im Voraus*.

---

## Gebäude

| Gebäude | Kosten | Funktion |
|---|---|---|
| **Stadtzentrum** | 275 Holz, 100 Stein | Dorfbewohner, Zeitalter-Aufstieg, Abgabestelle, +5 Bev. |
| **Haus** | 25 Holz | +5 Bevölkerung |
| **Mühle** | 100 Holz | Abgabestelle Nahrung, Farm-Technologien |
| **Holzfällerlager** | 100 Holz | Abgabestelle Holz |
| **Bergbaulager** | 100 Holz | Abgabestelle Gold + Stein |
| **Farm** | 60 Holz | 175 Nahrung, danach neu bestellen |
| **Kaserne** | 175 Holz | Infanterie |
| **Schießstand** | 175 Holz | Bogenschützen, Skirmisher |
| **Stall** | 175 Holz | Kavallerie |
| **Belagerungswerkstatt** | 200 Holz | Rammbock, Mangonel, Trebuchet |
| **Schmiede** | 150 Holz | Angriffs- und Rüstungs-Upgrades |
| **Universität** | 200 Holz | Verteidigungs- und Belagerungstechnologien |
| **Kloster** | 175 Holz | Mönche, Reliquien lagern |
| **Markt** | 175 Holz | Ressourcentausch, Handelskarren |
| **Burg** | 650 Stein | Einzigartige Einheit, sehr starke Verteidigung, Trebuchet |
| **Wachturm / Bergfried** | 50 Holz, 125 Stein | Verteidigung, Sichtweite |
| **Palisadenmauer** | 2 Holz | Billige frühe Absperrung |
| **Steinmauer** | 5 Stein | Ernsthafte Verteidigung |
| **Wunder** | 1000 je Holz/Stein/Gold | Alternative Siegbedingung |

**Garnison:** Einheiten können sich in Gebäude zurückziehen. Türme und Burgen schießen mit
mehr Pfeilen, je mehr Einheiten drin sind — Dorfbewohner in einem Turm sind eine echte
Verteidigungstaktik. Das Stadtzentrum bietet Dorfbewohnern bei einem Angriff Schutz.

**Reparatur:** Dorfbewohner reparieren beschädigte Gebäude und Belagerungswaffen.

---

## Einheiten und das Konter-System

Kampf ist Stein-Schere-Papier, nicht „mehr Einheiten gewinnt". Das Konter-System läuft über
**Angriffsboni gegen Einheitenklassen**, nicht über Sonderregeln.

```
              Infanterie
            ↗            ↘
   Bogenschützen  ←—  Kavallerie
            ↖            ↙
            Speerkämpfer
```

### Die Rollen

| Klasse | Stark gegen | Schwach gegen | Kosten-Profil |
|---|---|---|---|
| **Infanterie** (Milizen, Schwertkämpfer) | Gebäude, Belagerung | Bogenschützen, Kavallerie | Nahrung + Gold, billig |
| **Speerkämpfer / Pikeniere** | **Kavallerie** (riesiger Bonus) | Bogenschützen, Infanterie | Nahrung + Holz, **kein Gold** |
| **Bogenschützen** | Infanterie | Skirmisher, Kavallerie | Holz + Gold |
| **Skirmisher** | **Bogenschützen** | alles im Nahkampf | Holz + Nahrung, **kein Gold** |
| **Kavallerie** (Ritter) | Bogenschützen, Belagerung, Dorfbewohner | Speerkämpfer, Kamele | Nahrung + Gold, teuer |
| **Kamelreiter** | **Kavallerie** | Infanterie, Bogenschützen | Nahrung + Gold |
| **Rammbock** | **Gebäude** | Alles außer Gebäuden | Holz + Gold |
| **Mangonel / Onager** | Massen von Fernkämpfern | Kavallerie, Nahkampf | Holz + Gold |
| **Trebuchet** | Gebäude auf große Distanz | alles Bewegliche | Holz + Gold |
| **Mönch** | bekehrt gegnerische Einheiten | alles | Gold |

Die beiden **goldfreien** Klassen (Speerkämpfer, Skirmisher) sind der Grund, warum das
Endspiel funktioniert: Wer kein Gold mehr hat, kann immer noch *diese* bauen — und ist
damit nicht wehrlos, aber deutlich im Nachteil.

### Beispielwerte (Richtwerte)

| Einheit | HP | Angriff | Rüstung (Nah/Fern) | Reichweite | Bonus |
|---|---|---|---|---|---|
| Dorfbewohner | 25 | 3 | 0 / 0 | — | — |
| Späher | 45 | 3 | 0 / 2 | — | schnell, große Sicht |
| Miliz | 40 | 4 | 0 / 1 | — | — |
| Speerkämpfer | 45 | 3 | 0 / 0 | — | **+15 gegen Kavallerie** |
| Bogenschütze | 30 | 4 | 0 / 0 | 4 | — |
| Skirmisher | 30 | 2 | 0 / 3 | 4 | **+3 gegen Bogenschützen** |
| Ritter | 100 | 10 | 2 / 2 | — | — |
| Kamelreiter | 100 | 5 | 0 / 0 | — | **+9 gegen Kavallerie** |
| Rammbock | 175 | 2 | −3 / 180 | — | **+125 gegen Gebäude** |
| Mangonel | 50 | 40 | 0 / 6 | 7 (min. 3) | Flächenschaden |
| Trebuchet | 150 | 200 | 1 / 150 | 16 | nur gegen Gebäude sinnvoll |
| Mönch | 30 | — | 0 / 0 | 9 | bekehrt |

Man sieht daran gut, wie das System funktioniert: Ein Speerkämpfer hat **3** Grundangriff —
gegen Infanterie fast nutzlos. Gegen einen Ritter sind es **3 + 15 = 18**, und der Ritter
fällt sehr schnell. Der Bonus ist der Konter, nicht ein Sonderfall im Code.

---

## Die Schadensformel

Das ist der Mechanismus, der aus „Zahlen" ein Konter-System macht. Er ist einfach und
lässt sich exakt so implementieren:

Jede Einheit hat:
- eine Liste von **Angriffswerten pro Klasse** (z. B. `Nahkampf: 3`, `gegen Kavallerie: 15`)
- eine Liste von **Rüstungswerten pro Klasse** (z. B. `Nahkampf: 2`, `Fernkampf: 2`,
  `Klasse Kavallerie: 0`)

Der Schaden ist:

```
schaden = 0
für jede Angriffsklasse A des Angreifers:
    schaden += max(0, A.wert - verteidiger.rüstung[A.klasse])

schaden = max(1, schaden)      // es geht immer mindestens 1 Schaden durch
```

**Beispiel — Speerkämpfer schlägt Ritter:**

| Klasse | Angriff | Rüstung des Ritters | Beitrag |
|---|---|---|---|
| Nahkampf | 3 | 2 | 1 |
| gegen Kavallerie | 15 | 0 | 15 |
| **Summe** | | | **16** |

**Beispiel — Speerkämpfer schlägt Bogenschütze:**

| Klasse | Angriff | Rüstung | Beitrag |
|---|---|---|---|
| Nahkampf | 3 | 0 | 3 |
| gegen Kavallerie | 15 | *(kein Kavallerist)* | 0 |
| **Summe** | | | **3** |

Dieselbe Einheit macht 16 oder 3 Schaden — allein durch die Klassenzugehörigkeit des Ziels.
Deshalb reicht ein einzelnes `attackPower`-Feld nicht: Es braucht **Angriffsklassen und
Rüstungsklassen als Listen**.

Ein weiterer Punkt: Die Klasse „gegen Gebäude" ist der Grund, warum Rammböcke funktionieren.
Ihr Grundangriff ist **2** — lächerlich gegen Einheiten, verheerend gegen Mauern.

---

## Technologien

Technologien sind permanente Verbesserungen, die in Gebäuden erforscht werden. Sie sind der
zweite große Ressourcen-Abfluss neben Einheiten.

### Wirtschaftstechnologien (die wichtigsten)

| Technologie | Gebäude | Wirkung |
|---|---|---|
| **Leinenkleidung** | Stadtzentrum | Dorfbewohner +15 HP, mehr Rüstung |
| **Schubkarre** | Stadtzentrum | Dorfbewohner arbeiten und laufen schneller, tragen mehr |
| **Handkarren** | Stadtzentrum | dasselbe nochmal, stärker |
| **Kummet / Schwerer Pflug / Fruchtwechsel** | Mühle | Farm-Ertrag 175 → 250 → 375 → 550 |
| **Doppelte Axt / Bogensäge** | Holzfällerlager | Holzfällen schneller |
| **Goldschürfen / Goldschacht** | Bergbaulager | Gold schneller |

Die Schubkarre ist eine der am meisten unterschätzten Technologien: Sie wirkt auf *jeden*
Dorfbewohner gleichzeitig und zahlt sich über die restliche Partie aus.

### Militärtechnologien

Die **Schmiede** bietet je drei Stufen für:
- Angriff Nahkampf / Angriff Fernkampf
- Rüstung Infanterie / Rüstung Kavallerie / Rüstung Fernkämpfer

Jede Stufe gibt +1. Das klingt wenig — aber bei der obigen Schadensformel ist der Unterschied
zwischen `4 − 0` und `4 − 3` gewaltig: **+3 Rüstung reduziert den Schaden um 75 %.**
Deshalb entscheiden Schmiede-Upgrades Schlachten stärker als Einheitenzahl.

Dazu kommen **Elite-Upgrades** (Miliz → Schwertkämpfer → Langschwert → …), die HP und
Angriff deutlich erhöhen, und die beiden **einzigartigen Technologien** jeder Zivilisation
in der Burg.

---

## Mönche und Bekehrung

Mönche sind eine eigene Mechanik und nicht bloß „Heiler":

- Ein Mönch **bekehrt** gegnerische Einheiten — sie wechseln dauerhaft die Seite.
- Die Bekehrung braucht Zeit (mehrere Sekunden) und hat ein Zufallselement; sie kann
  scheitern und muss neu beginnen.
- Sie hat eine **Reichweite** und wird abgebrochen, wenn sich das Ziel weit genug entfernt.
- **Gebäude und Belagerungswaffen** sind meist immun oder brauchen mehrere Mönche.
- Mönche **heilen** eigene Einheiten außerhalb des Kampfes.
- Sie sammeln **Reliquien** ein und bringen sie ins Kloster — jede eingelagerte Reliquie
  erzeugt kontinuierlich **Gold**. Im späten Spiel ist das eine der letzten Goldquellen.

Gegenmittel: Mönche haben 30 HP und sind sofort tot, wenn sie erreicht werden. Die
Technologie *Glaube* macht eigene Einheiten schwerer bekehrbar.

---

## Sichtbarkeit und Nebel des Krieges

Drei Zustände pro Kachel, und der Unterschied zwischen ihnen prägt das Spielgefühl stark:

| Zustand | Darstellung | Was man sieht |
|---|---|---|
| **Unerforscht** | schwarz | nichts |
| **Erforscht** | abgedunkelt | Gelände und Gebäude — im **letzten bekannten Zustand** |
| **Sichtbar** | normal | alles, inklusive gegnerischer Einheiten in Echtzeit |

Der Kniff liegt beim mittleren Zustand: Man sieht dort ein Gebäude, das der Gegner vielleicht
längst abgerissen hat. Das erzeugt Unsicherheit und macht **Aufklärung** zu einer laufenden
Aufgabe, nicht zu einer einmaligen Aktion.

Jede Einheit hat eine eigene **Sichtweite**; Späher und Türme sehen besonders weit.
Deshalb ist der Späher zu Spielbeginn so wichtig — er ist im Wortsinn das einzige, was man
über den Gegner weiß.

---

## Bewegung, Wegfindung, Formationen

Der Bereich, an dem RTS-Klone üblicherweise scheitern.

- **Wegfindung:** A\* auf dem Kachelgitter, plus eine feinere Ausweichlogik, damit Einheiten
  sich nicht gegenseitig blockieren.
- **Gruppenbewegung:** Eine ausgewählte Gruppe bewegt sich als Verband — nicht alle auf
  denselben Punkt, sondern in einer Formation um das Ziel herum.
- **Formationen:** Linie, Kasten, Gestaffelt, Flanke. Die praktische Wirkung: Nahkämpfer
  vorne, Fernkämpfer und Belagerung geschützt dahinter.
- **Gemeinsame Geschwindigkeit:** Ein Verband bewegt sich so schnell wie seine langsamste
  Einheit, sonst reißt er auseinander.
- **Haltungen:** Aggressiv (greift alles an), Defensiv (bleibt in der Nähe), Stellung halten,
  Kein Angriff. Bestimmt, ob Einheiten selbstständig auf Feindkontakt reagieren.

Wichtiger als perfekte Wegfindung ist, dass Einheiten **nicht stecken bleiben** und
**nicht zittern**. Beides fällt Spielern sofort auf.

---

## Die Karte

- Quadratisches Kachelgitter, typischerweise **120×120 bis 220×220** Kacheln je nach Größe.
- Geländetypen: Gras, Sand, Schnee, Wasser, Untiefen, Straße. Sie beeinflussen vor allem
  Begehbarkeit und Bebaubarkeit.
- **Erhöhungen** geben einen Angriffsbonus nach unten und Sichtvorteil.
- Standardkarte **„Arabia"**: offen, wenig Wasser, wenig natürliche Engstellen — deshalb die
  kompetitive Standardkarte, weil sie niemanden bevorzugt.
- Andere bekannte Typen: **Black Forest** (dichter Wald, enge Pässe → Verteidigungsspiel),
  **Islands** (nur per Schiff erreichbar), **Nomad** (Start ohne Stadtzentrum).

**Startaufstellung (Standard):** 3 Dorfbewohner, 1 Späher, 1 Stadtzentrum;
200 Nahrung, 200 Holz, 100 Gold, 200 Stein. In der Nähe liegen immer 8 Schafe, 2 Wildschweine,
Beerenbüsche, Wald, eine Goldmine und ein Steinbruch — die Karte wird so generiert, dass
**jeder Spieler dieselben Startressourcen** in Reichweite hat.

---

## Ablauf einer Partie

Eine typische 1-gegen-1-Partie (AoE II, Karte „Arabia") dauert **25–45 Minuten**
und läuft fast immer nach demselben Muster ab. Die Zeitangaben sind Richtwerte für
solide Spieler — Profis sind 1–2 Minuten schneller.

### Minute 0–2 — Start

Man startet mit einem **Stadtzentrum**, **3 Dorfbewohnern** und einem **Späher**.
Sofort passieren drei Dinge parallel:

- Das Stadtzentrum produziert **ununterbrochen** weitere Dorfbewohner (das hört bis zur
  Ritterzeit nie auf).
- Die ersten Dorfbewohner gehen auf **Schafe** (Nahrung, direkt neben dem Stadtzentrum).
- Der Späher erkundet die Karte: eigene Ressourcen finden, dann den Gegner suchen.

### Minute 2–9 — Dunkle Zeit (Wirtschaftsaufbau)

Reiner Wirtschaftsaufbau, praktisch kein Militär. Eine typische **Build Order** sieht so aus:

| Dorfbewohner | Aufgabe |
|---|---|
| 1–4 | Schafe |
| 5–8 | Holz (Holzfällerlager bauen) |
| 9–10 | Wildschwein locken und sammeln |
| 11–14 | Beeren (Mühle bauen) |
| 15–17 | Holz (zweites Lager) |
| 18–21 | Nahrung / Gold |
| ab 22 | Aufstieg in die Feudalzeit einleiten |

Diese Reihenfolge ist auswendig gelernt und wird kaum improvisiert. Der Aufstieg dauert
~2 Minuten, in denen nichts Neues verfügbar ist.

### Minute 9–16 — Feudalzeit (erste Entscheidung)

Jetzt trennen sich die Wege:

| Strategie | Idee | Risiko |
|---|---|---|
| **Rush** | Sofort Militär (Bogenschützen, Speerkämpfer) und den Gegner früh stören | Verliert Wirtschaft, wenn der Angriff scheitert |
| **Fast Castle** | Militär überspringen, direkt weiter in die Ritterzeit | Extrem verwundbar gegen einen Rush |
| **Boom** | Zusätzliche Stadtzentren, maximale Wirtschaft | Verliert, wenn Druck kommt bevor es sich auszahlt |

Parallel laufen die ersten Schmiede-Upgrades und der Ausbau der Holz- und Goldwirtschaft.
Wer angegriffen wird, muss **Mauern und Türme** ziehen — und verliert dabei Wirtschaftszeit.

### Minute 16–25 — Ritterzeit (das Herzstück)

Die längste und wichtigste Phase. Hier wird der Großteil der Partien entschieden:

- **Burgen** werden gebaut → einzigartige Einheiten werden verfügbar
- Vollwertige Armeen (Ritter, Armbrustschützen, Mönche, Rammböcke)
- Erste echte Schlachten um **Kartenkontrolle** — vor allem um Goldminen
- Die Wirtschaft läuft auf Vollast (100–140 Bevölkerung)

Typischer Ablauf: Angriff → Konterarmee bauen → Rückzug → nachrüsten → nächster Angriff.
Wer eine Schlacht mit der falschen Armeezusammensetzung verliert, verliert oft die Partie.

### Minute 25+ — Imperialzeit (Endgame)

- Elite-Upgrades für alle Einheiten, **Trebuchets** und schweres Belagerungsgerät
- Dorfbewohner werden zunehmend zu Militär umgeschichtet
- Das **Gold geht aus** — wer keine Goldminen mehr hält, kann nur noch goldfreie Einheiten
  bauen (Speerkämpfer, Skirmisher) und ist militärisch im Nachteil
- Genau das erzwingt die Entscheidung: Wer bis hierhin die Karte kontrolliert, gewinnt meist

### Das Ende

1. **Aufgabe** — mit Abstand der häufigste Fall. Sobald ein Spieler wirtschaftlich
   aussichtslos zurückliegt, wird aufgegeben statt ausgespielt.
2. **Vernichtung** — alle Gebäude und Einheiten des Gegners zerstört.
3. **Alternativer Sieg** — Wunder oder Reliquien halten (selten im kompetitiven Spiel).

---

## Siegbedingungen und Spielmodi

### Siegbedingungen

- **Eroberung** — alle Gegner ausschalten (Standard)
- **Wunder** — ein Wunder bauen und eine festgelegte Zeit halten (die Uhr ist für alle
  sichtbar, das Wunder wird sofort zum Angriffsziel)
- **Reliquien** — alle Reliquien der Karte sammeln und halten
- **Punktsieg / Zeitlimit** — je nach Einstellung

### Spielmodi

- **Standard** — der oben beschriebene Ablauf
- **Todesmatch** — Start mit riesigen Ressourcenmengen, sofort Endgame
- **Regicide** — jeder hat einen König; stirbt er, ist die Partie verloren
- **Kampagnen** — vorgeskriptete Einzelspieler-Missionen mit historischer Handlung,
  oft mit festen Vorgaben statt freiem Aufbau
- **Teamspiele (2v2 bis 4v4)** — Ressourcen können an Verbündete geschickt werden,
  Positionen auf der Karte (Flanke vs. Mitte) bestimmen die Rolle

---

## Zivilisationen

Jedes Volk hat:

1. einen **Wirtschafts- oder Militärbonus** (z. B. „Dorfbewohner sammeln 15 % schneller")
2. eine **einzigartige Einheit**, nur in der Burg baubar
3. zwei **einzigartige Technologien** (Ritterzeit und Imperialzeit)
4. einen **Teambonus**, der auch für Verbündete gilt
5. einen **eingeschränkten Technologiebaum** — niemand hat alles

| Zivilisation | Einzigartige Einheit | Charakter |
|---|---|---|
| Briten | Langbogenschütze | Fernkampf, große Reichweite |
| Franken | Wurfaxtkrieger | Kavallerie, billige Burgen |
| Mongolen | Mangudai | Berittene Bogenschützen, schnell |
| Goten | Huskarl | Billige Infanterie in Massen |
| Azteken | Jaguarkrieger | Starke Infanterie, keine Kavallerie |
| Türken | Janitschar | Schießpulver, starkes Gold |

AoE II: DE hat inzwischen über 40 Zivilisationen. Der eingeschränkte Technologiebaum ist
dabei das eigentliche Balancing-Werkzeug: Dass die Azteken **gar keine Kavallerie** haben,
prägt ihr Spiel stärker als jeder Prozentbonus.

---

## Die Benutzeroberfläche

Das klassische AoE-II-Layout (siehe `ImSpiel1.jpg` / `ImSpiel2.jpg`) besteht aus drei
Bereichen, die seit 1999 praktisch unverändert sind:

### Oben — Ressourcenleiste

Eine schmale Leiste über die volle Breite:

```
[Holz 1260] [Nahrung 1082] [Gold 76] [Stein 850] [Bevölkerung 117/75]   « Imperialzeit »
```

Jede Ressource mit Icon und Zahl, die Bevölkerung als `aktuell/limit`, und mittig der Name
des aktuellen Zeitalters. Rot eingefärbte Bevölkerung = Limit erreicht.

### Unten — Kommandoleiste

Ein breites Holz-/Pergament-Panel über die volle Breite, dreigeteilt:

| Bereich | Inhalt |
|---|---|
| **links** | Raster aus Aktions-Icons — was die aktuelle Auswahl tun kann (Gebäude bauen, Einheiten ausbilden, Technologien, Haltung, Abbrechen) |
| **mitte** | Info zur Auswahl: Name, Portrait, HP-Balken, Angriff/Rüstung, bei Gebäuden die Produktionswarteschlange |
| **rechts** | **Minimap** in derselben Ansicht wie die Spielkarte (im Spiel zurzeit Draufsicht, Norden oben), mit Geländefarben, eigenen (blau) und gegnerischen (rot) Einheiten und dem aktuellen Sichtausschnitt als weißes Rechteck |

### Auswahl in der Spielwelt

- **Linksklick** wählt eine Einheit, **Ziehen** ein Rechteck für mehrere
- **Doppelklick** wählt alle sichtbaren Einheiten desselben Typs
- **Rechtsklick** ist der Universalbefehl: auf Boden = hingehen, auf Ressource = sammeln,
  auf Feind = angreifen, auf eigenes Gebäude = betreten
- Ausgewählte Einheiten bekommen einen **Auswahlring** unter den Füßen und einen HP-Balken
- **Strg+Zahl** legt Kontrollgruppen an

Das Prinzip dahinter: **ein Mauszeiger, kontextabhängige Bedeutung.** Der Spieler muss
nie einen Modus umschalten.

---

## Isometrische Darstellung

AoE I–III zeichnen die Welt isometrisch: Das quadratische Kachelgitter wird um 45° gedreht
und vertikal gestaucht, sodass jede Kachel als **Raute** erscheint — üblicherweise im
Verhältnis **2:1** (z. B. 64×32 Pixel).

Die Umrechnung von Gitter- auf Bildschirmkoordinaten:

```
screenX = (gridX - gridY) * (kachelBreite / 2)
screenY = (gridX + gridY) * (kachelHöhe  / 2)
```

und zurück:

```
gridX = (screenY / (kachelHöhe/2) + screenX / (kachelBreite/2)) / 2
gridY = (screenY / (kachelHöhe/2) - screenX / (kachelBreite/2)) / 2
```

**Zeichenreihenfolge:** Von hinten nach vorne, sortiert nach `gridX + gridY`. Nur so
überlappen näher stehende Objekte korrekt die weiter hinten stehenden. Bei Objekten, die
mehrere Kacheln belegen (Gebäude), zählt die vorderste Kachel.

**Praktische Konsequenzen:**

- Sprites brauchen mehrere Blickrichtungen (klassisch 8), sonst wirken Einheiten falsch
  gedreht.
- Der Ursprung eines Sprites liegt an seinem **Fußpunkt**, nicht in der Bildmitte.
- Die Kamera bewegt sich in Bildschirmkoordinaten, das Kachelgitter aber diagonal — was
  sich beim Scrollen anfangs ungewohnt anfühlt.
- Mausklicks müssen mit der Rückrechnung oben in Gitterkoordinaten übersetzt werden,
  nicht durch eine simple Division.

---

## Warum die Reihe funktioniert

- **Wirtschaft und Militär sind gleich wichtig.** Das unterscheidet AoE von StarCraft-artigen
  Spielen, in denen der Kampf dominiert.
- **Hohe Skill-Decke, niedrige Einstiegshürde.** Man kann gegen die KI gemütlich bauen oder
  auf Turnierniveau um Sekunden in der Build Order kämpfen.
- **Endliche Ressourcen erzwingen Entscheidungen.** Ohne auslaufendes Gold gäbe es keinen
  Grund, die Karte zu umkämpfen.
- **Historisches Setting mit echtem Wiedererkennungswert.** Kampagnen erzählen reale
  Geschichte (Jeanne d'Arc, Saladin, Dschingis Khan).
- **Sehr lange Lebensdauer.** AoE II wird seit über 25 Jahren gespielt und hat eine
  aktive Turnier- und Streaming-Szene.

---

## Relevanz für dieses Projekt

**Stand am 15.08.2026:** Der Code in `AgeOfEvolutions/` war ursprünglich das unveränderte
MonoGame-„Platformer 2D"-Beispiel — ein Jump'n'Run mit Springen, Gems und Ausgang.
`game_plan.md` beschreibt dagegen einen RTS. Inzwischen ist umgestellt worden:
`MainMenuScreen` zeigt ein AoE-artiges Menü über `Content/Backgrounds/menu.png`,
und „Neues Spiel" führt in `RTSGameplayScreen` (Tilemap, Dorfbewohner, Auswahl,
Ressourcen). Der Platformer-Code (`Game/Level.cs`, `Game/Player.cs`, `Content/Levels/`)
liegt noch im Projekt, ist aber nicht mehr über das Menü erreichbar.

### Reihenfolge der Umsetzung

Nach Wichtigkeit für das Spielgefühl, nicht nach Aufwand:

1. **Dorfbewohner-Loop** — laufen, sammeln, Traglast 10, Abgabestelle, zurück, automatisch
   fortsetzen. Ohne den fühlt sich nichts nach AoE an.
2. **Vier getrennte Ressourcen** statt einer generischen — erzeugt erst die Entscheidungen.
3. **Bevölkerungslimit** als gemeinsame Obergrenze für Wirtschaft *und* Armee, plus Häuser.
4. **Zeitalter als Gate** für Gebäude und Einheiten — der Motor des Spielverlaufs.
5. **Schadensformel mit Angriffs- und Rüstungsklassen** (siehe oben) statt eines einzelnen
   `AttackPower`-Werts. Das ist eine kleine Änderung mit großer Wirkung.
6. **Endliche Gold- und Steinvorkommen.** Ohne sie hat eine Partie kein natürliches Ende.
7. **Isometrische Darstellung** mit der Projektion und Tiefensortierung oben.
8. **Fog of War** mit den drei Zuständen — vor allem der „erforscht, aber veraltet"-Zustand.
9. **A\*-Wegfindung mit Gruppenbewegung.** Der Punkt, an dem RTS-Klone üblicherweise
   scheitern — wichtiger ist „bleibt nicht stecken" als „findet den optimalen Weg".
10. **HUD** in drei Teilen: Ressourcenleiste oben, Kommandoleiste und Minimap unten.

### Bewusst nicht nötig für ein überzeugendes Grundgerüst

Zivilisationsboni, Reliquien, Mönche, Handel, Seekampf, Formationen, Kampagnen. Das ist
Content und Feinschliff — es macht das Spiel reicher, aber es macht es nicht erst zu einem
Age-of-Empires-Spiel.

### Bekannter Engpass

Im Projekt existiert derzeit **kein einziges AoE-taugliches Grafik-Asset**. Der
`RTSGameplayScreen` zeichnet farbige 32×32-Quadrate. Punkte 1–6 und 8–9 der Liste lassen
sich damit vollständig umsetzen und testen — Punkt 7 und 10 brauchen Kachel- und
Einheiten-Sprites, sonst bleibt es bei Farbflächen, egal wie korrekt die Projektion ist.

---

*Erstellt am 14.08.2026, erweitert am 15.08.2026*
