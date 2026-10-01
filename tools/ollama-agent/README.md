# Lokale Ollama-Agents

Verteilt Punkte aus `TODO.md` an ein lokal laufendes Ollama-Modell und nimmt das
Ergebnis maschinell ab.

```
Snapshot  ->  Agent laeuft  ->  post_steps  ->  Verifikation  ->  behalten / zurueckrollen
```

Der entscheidende Punkt: **die Verifikation laeuft im Orchestrator, nicht im Agent.**
Das Modell hat keinen Shell-Zugriff, kann also weder bauen noch testen — und damit
seine eigene Abnahme nicht faelschen. Schlaegt die Abnahme fehl, wird der Snapshot
zurueckgespielt und die Aufgabe bleibt offen.

## Aufruf

```bash
py run_tasks.py --list                      # Aufgaben anzeigen
py run_tasks.py --tasks H3 D2               # bestimmte Aufgaben
py run_tasks.py --auto                      # alle mit "auto": true
py run_tasks.py --tasks C3 --num-ctx 131072 # kleineres Kontextfenster, spart VRAM
py run_tasks.py --tasks H3 --dry-run        # nur anzeigen, was liefe
py run_tasks.py --recheck                   # alle gepflegten Abnahmen erneut, ohne Agent
```

Voraussetzung: Ollama laeuft auf `localhost:11434` und das Modell ist verfuegbar.
Standard ist `qwen3.8:27b` (dicht, 27B, Q4_K_M), anderes per `--model` — etwa
`qwen3.6:35b`, das bis 2026-09-30 Standard war.

## Kontextfenster

Standard ist **262144 Token (256K)**. Mehr geht nicht, aus zwei voneinander
unabhaengigen Gruenden:

- **Trainingsmaximum.** `qwen3.8:27b` ist auf 262144 Token trainiert
  (`/api/show`, Feld `qwen35.context_length`). Groessere Werte reichen ueber die
  trainierten Positionen hinaus; eine YaRN-Skalierung bietet Ollama nicht an.
- **VRAM.** Gemessen am 2026-09-30 auf der RTX 5090 (32 GB) mit
  `OLLAMA_KV_CACHE_TYPE=q8_0` und `OLLAMA_FLASH_ATTENTION=1`, Werte aus dem Ollama-Log:

  | num_ctx | KV-Cache Modell + MTP-Entwurf | Prognose gesamt |
  |---|---|---|
  | 131072 | 4352 + 512 MiB | 21867 MiB |
  | 262144 | 8704 + 1024 MiB | 27499 MiB |

  Bei 262144 liegen alle 66 Schichten auf der GPU, danach sind knapp 400 MiB frei.
  524288 braeuchte rund 37000 MiB und wuerde teilweise auf die CPU ausgelagert.

Die Karte ist bei 256K also praktisch voll. Laeuft nebenher etwas mit nennenswertem
VRAM-Bedarf, etwa das Spiel selbst, wird das Modell spuerbar langsamer — dann
`--num-ctx 131072`. Ist das Modell bereits mit einer anderen Groesse geladen, etwa
von einem anderen Werkzeug, laedt Ollama es neu (aus dem Dateicache rund 6 s).

## Werkzeuge des Agents

| Werkzeug | Zweck |
|---|---|
| `read_file` | Datei mit Zeilennummern lesen |
| `list_dir` | Verzeichnis auflisten (ohne `bin`, `obj`, `.vs`, `.git`) |
| `replace_in_file` | chirurgischer Textersatz — **bevorzugt** |
| `write_file` | Datei komplett neu schreiben — nur fuer neue/kleine Dateien |
| `delete_file` | Datei loeschen — nur Pfade aus der Freigabeliste |
| `rename_file` | Datei umbenennen, auch reine Gross-/Kleinschreibung |
| `finish` | fertig, Abnahme starten |

Kein Shell-Zugriff, keine Netzwerkzugriffe, kein Git.

## Schutzmechanismen

- **Freigabeliste je Aufgabe** (`allow_write`) — Schreibzugriff nur auf die dort
  genannten Muster, alles andere wird abgelehnt.
- **Pfadsperre** — Schreibversuche ausserhalb der Repo-Wurzel werden abgewehrt und
  dem Modell als Fehler gemeldet, ohne den Lauf abzubrechen.
- **Git-Sperre** — `run_command` lehnt alles mit `commit`, `push`, `reset --hard`,
  `clean -fd` oder `rebase` ab. Der Harness fasst die Git-History nicht an.
- **Snapshot + Rueckrollen** — bei fehlgeschlagener Abnahme wird der Ausgangszustand
  wiederhergestellt.
- **`.rejected/`** — die abgelehnte Fassung wird vorher aufbewahrt. Ohne das laesst
  sich hinterher nicht unterscheiden, ob das Modell Unsinn gebaut hat oder das
  Abnahmekriterium zu eng war.

## Eine Aufgabe hinzufuegen

In `tasks.json`:

```json
{
  "id": "H9",
  "title": "Kurzer Titel",
  "auto": false,
  "max_turns": 20,
  "prompt": "Praezise Anweisung. Dateien und Zielzustand konkret benennen.",
  "context_files": ["pfad/zur/datei.cs"],
  "allow_write": ["pfad/zur/datei.cs"],
  "verify": ["dotnet build ...", "dotnet test ..."]
}
```

Die IDs entsprechen denen in `TODO.md`.

## Pruefskripte

Abnahmen, die mehr als eine Zeile brauchen, liegen als Skripte in `checks/` und
laufen per `"verify": ["py tools/ollama-agent/checks/<name>.py"]` mit der
Repo-Wurzel als Arbeitsverzeichnis. `_cs.py` findet C#-Methoden, `_todo.py`
Abschnitte und Checkboxen in `TODO.md`.

- **Vor dem Lauf gegen den Altstand pruefen** — die Abnahme muss dort scheitern,
  und zwar aus dem richtigen Grund.
- **Und gegen jede Stelle, die sie finden soll.** Der erste Methoden-Finder suchte die
  Parameterliste per `\([^)]*\)` und scheiterte an `List<(int x, int y)>` und an
  Ausdrucksruempfen (`=> ...;`) — zwei korrekte Ergebnisse (C1n, C1c) wurden verworfen.
- **Nur Code pruefen, keinen Fliesstext.** Eine Sperre gegen „MonoGame" traf den
  Doku-Kommentar „reine Logik ohne MonoGame".
- **Nach jeder Runde `--recheck`.** Am 2026-10-01 waren drei Pruefungen (d7, d8a, e5)
  still gebrochen: spaetere Aufgaben hatten ein Datum, eine Testzahl und die Schreibweise
  `Width = 4` zu Recht geaendert, und die Abschlusspruefung lief nur ueber eine Auswahl.
  `--recheck` fuehrt jede gepflegte Abnahme aus `tasks.json` genau so aus wie der
  Harness - mit Argumenten, aus der Repo-Wurzel, Erfolg nach Exit-Code.
- **Doku-Pruefungen sind Momentaufnahmen.** Sie sichern die Abnahme einer Aufgabe; eine
  spaetere Aufgabe darf sie brechen. Zeilenzahlen deshalb messen (siehe
  `d9_projekt_struktur.py`), nicht abschreiben.

## Erfahrungen aus dem ersten Lauf

Drei Dinge, die beim Aufsetzen schiefgingen — alle drei lagen am Harness, nicht am Modell:

1. **Zeilennummern zerstoeren den Textersatz.** `read_file` gab `{nr:5d}␣␣{code}` aus.
   Das Modell entfernte die Nummer, behielt aber die zwei Trennleerzeichen — und traf
   deshalb mit `replace_in_file` *nie*: 20 Fehlversuche in Folge bei C3. Abhilfe: das
   Trennzeichen ist jetzt ` | ` und damit eindeutig, zusaetzlich faellt `replace_in_file`
   auf einen einrueckungstoleranten Abgleich zurueck und setzt den Ersatztext auf die
   tatsaechliche Einrueckung der Fundstelle um. Danach: 2 Treffer auf Anhieb, 26 s.

2. **`git check-ignore` ueberspringt getrackte Dateien.** Die Pruefung der `.gitignore`
   meldete "nicht ignoriert", obwohl die Regel griff, weil `bin/` und `obj/` bereits
   getrackt sind. Korrekte `.gitignore` wurde grundlos verworfen. Abhilfe: `--no-index`.

3. **Zu enge Textpruefungen verwerfen korrekte Arbeit.** Die C3-Abnahme verlangte
   woertlich `ResourceAmount--`; `-= 1` waere genauso richtig gewesen. Abnahmekriterien
   sollten die *Eigenschaft* pruefen, nicht eine Schreibweise.

Kurz: ein fehlgeschlagener Lauf ist zuerst ein Verdacht gegen das Abnahmekriterium,
nicht gegen das Modell. Deshalb `.rejected/`.

## Grenzen

Mechanische, eng umrissene Aufgaben mit harter Abnahme erledigt das Modell zuverlaessig.
Architekturaenderungen — in `TODO.md` mit `[X]` markiert, etwa das Zusammenfuehren von
`Unit` und `UnitEntity` — gehoeren nicht hierher: sie brauchen Entwurfsentscheidungen,
und ein Build-gruenes Ergebnis ist dafuer kein ausreichender Nachweis.
