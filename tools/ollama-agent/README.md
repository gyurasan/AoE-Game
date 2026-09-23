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
py run_tasks.py --tasks C3 --num-ctx 262144 # groesseres Kontextfenster
py run_tasks.py --tasks H3 --dry-run        # nur anzeigen, was liefe
```

Voraussetzung: Ollama laeuft auf `localhost:11434` und das Modell ist verfuegbar.
Standard ist `qwen3.6:35b`, anderes per `--model`.

## Kontextfenster

`qwen3.6:35b` kann maximal **262144 Token (256K)** — nachzulesen ueber
`/api/show` im Feld `qwen35moe.context_length`. Der Standard hier ist 262144.
Groesserer Kontext heisst mehr VRAM fuer den KV-Cache; ist das Modell bereits mit
einer anderen Groesse geladen, laedt Ollama es neu. Mit `OLLAMA_KV_CACHE_TYPE=q8_0`
passen die 256K zusammen mit dem 22-GB-Modell vollstaendig auf die GPU.

## Werkzeuge des Agents

| Werkzeug | Zweck |
|---|---|
| `read_file` | Datei mit Zeilennummern lesen |
| `list_dir` | Verzeichnis auflisten (ohne `bin`, `obj`, `.vs`, `.git`) |
| `replace_in_file` | chirurgischer Textersatz — **bevorzugt** |
| `write_file` | Datei komplett neu schreiben — nur fuer neue/kleine Dateien |
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
