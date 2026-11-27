# Musikliste Age of Evolutions

Stand: 2026-11-27. Bestellliste für die Spielmusik. Die Stücke liefert ein Künstler; diese Liste sagt, welche Stücke das Spiel braucht, wann sie laufen und welche technischen Vorgaben sie erfüllen sollen.

Das Spiel kennt drei Kartenarten (Europa, Ägypten, Wiking) und vier Zeitalter (Dunkle Zeit, Feudalzeit, Ritterzeit, Imperialzeit). Das Hauptmenü hat seine Musik schon: `tools/klang/Age of Evolutions - Main Theme.mp3` (Holzflöte, Laute, Hackbrett, Rahmentrommel, Hörner, Chor ohne Text). Daran sollen sich die neuen Stücke klanglich anlehnen.

## A. Standardmusik je Karte und Zeitalter (12 Stücke)

Jede Karte hat ihre eigenen Instrumente. Mit jedem Zeitalter wird die Musik voller und prächtiger: Das Dorf wächst hörbar mit. Das Stück wechselt, sobald man ins nächste Zeitalter aufsteigt.

| Nr. | Datei | Karte | Zeitalter | Charakter |
|---|---|---|---|---|
| 1 | `Europa_1_Dunkel.mp3` | Europa | Dunkle Zeit | karg, ruhig: Holzflöte, Laute, Bordun |
| 2 | `Europa_2_Feudal.mp3` | Europa | Feudalzeit | kleines Ensemble: Drehleier, Fidel, Rahmentrommel |
| 3 | `Europa_3_Ritter.mp3` | Europa | Ritterzeit | Hörner, Streicher, Pauken, stolzer |
| 4 | `Europa_4_Imperial.mp3` | Europa | Imperialzeit | volles Orchester, Chor ohne Text, majestätisch |
| 5 | `Aegypten_1_Dunkel.mp3` | Ägypten | Dunkle Zeit | Ney-Flöte, Oud, leise Handtrommel |
| 6 | `Aegypten_2_Feudal.mp3` | Ägypten | Feudalzeit | Qanun, Darbuka, Harfe |
| 7 | `Aegypten_3_Ritter.mp3` | Ägypten | Ritterzeit | Streicher, Messing, Rahmentrommeln |
| 8 | `Aegypten_4_Imperial.mp3` | Ägypten | Imperialzeit | volles Orchester mit orientalischen Skalen, Chor |
| 9 | `Wiking_1_Dunkel.mp3` | Wiking | Dunkle Zeit | Leier, Tagelharpa, Wind, tiefer Bordun |
| 10 | `Wiking_2_Feudal.mp3` | Wiking | Feudalzeit | Hardangerfiedel, Bukkehorn, Felltrommel |
| 11 | `Wiking_3_Ritter.mp3` | Wiking | Ritterzeit | tiefe Trommeln, Hörner, Männerchor summend |
| 12 | `Wiking_4_Imperial.mp3` | Wiking | Imperialzeit | episch nordisch, volles Orchester |

## B. Angriffsmusik je Karte (3 Stücke)

Sie läuft, solange man angegriffen wird: ab dem ersten Treffer auf eigene Einheiten oder Gebäude (derselbe Auslöser wie der rote Kreis auf der Minikarte). Ist es eine Weile ruhig (etwa 20–30 s ohne Treffer), geht es zurück zur Standardmusik aus A.

| Nr. | Datei | Karte | Charakter |
|---|---|---|---|
| 13 | `Angriff_Europa.mp3` | Europa | treibende Kriegstrommeln, schnelles Tempo, Spannung, Instrumente wie A |
| 14 | `Angriff_Aegypten.mp3` | Ägypten | Darbuka- und Rahmentrommel-Rhythmus, scharfe Bläser |
| 15 | `Angriff_Wiking.mp3` | Wiking | schwere Felltrommeln, Kriegshörner, Chorrufe |

## Kleinere Fassung

Sind 15 Stücke zu viel: vier Zeitalter-Stücke für alle Karten gleich und ein Angriffsstück, also fünf Stücke. Die Karte bekäme dann keine eigene Musik.

| Datei | Zeitalter / Lage |
|---|---|
| `Zeitalter_1_Dunkel.mp3` | Dunkle Zeit |
| `Zeitalter_2_Feudal.mp3` | Feudalzeit |
| `Zeitalter_3_Ritter.mp3` | Ritterzeit |
| `Zeitalter_4_Imperial.mp3` | Imperialzeit |
| `Angriff.mp3` | Angriff |

## Optional

Kurze Einspieler (3–8 s), falls Budget bleibt:

| Datei | Wann |
|---|---|
| `Einspieler_Sieg.mp3` | Spiel gewonnen |
| `Einspieler_Niederlage.mp3` | Spiel verloren |
| `Einspieler_Aufstieg.mp3` | Aufstieg ins nächste Zeitalter |

## Technische Vorgaben

- **Instrumental**, höchstens Chor ohne Text, passend zum Main Theme.
- **Format:** MP3 (192 kbit/s oder mehr) oder WAV, 44,1 oder 48 kHz, Stereo.
- **Länge:** Standardstücke 3–5 Minuten, Angriff 1,5–2,5 Minuten.
- **Nahtlos wiederholbar:** Die Stücke laufen in Dauerschleife. Das Ende sollte sauber an den Anfang passen und nicht lange ausklingen.
- **Angriff mit sofortigem Einstieg:** höchstens 1–2 s Intro. Er kann jederzeit einsetzen und wieder enden.
- **Gleich laut:** alle Stücke etwa −16 LUFS, Spitzen höchstens −1 dB. Das Main Theme ist recht laut gemastert (im Mittel −17,5 dB, Spitzen −2,3 dB); die neuen Stücke sollten daneben nicht springen.
- **Dateinamen** wie in den Tabellen, damit sich die Stücke ohne Rätselraten einbauen lassen.
