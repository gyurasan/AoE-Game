"""Schreibt die Aufgaben L1 (Kernregeln) und L2 (Baumenü, zweite Reihe, Bilder) nach
tools/ollama-agent/tasks.json. Das Modell programmiert nach Vertrag; Suchtexte nennen nur
die Einbindungsstellen. Fahnentücher kommen gemessen aus fahnen.json.
"""
import json
from pathlib import Path

HIER = Path(__file__).resolve().parent
ROOT = Path("D:/Apps/AgeOfEmpire")
SCREEN = "AgeOfEvolutions/AgeOfEvolutions.Core/Screens/RTSGameplayScreen.cs"

BUILD_GL = "dotnet build AgeOfEvolutions/AgeOfEvolutions.DesktopGL/AgeOfEvolutions.DesktopGL.csproj -v q --nologo"
BUILD_DX = "dotnet build AgeOfEvolutions/AgeOfEvolutions.WindowsDX/AgeOfEvolutions.WindowsDX.csproj -v q --nologo"
TEST = "dotnet test tests/AoE.Tests/AoE.Tests.csproj --nologo -v q"
ABLAUF = ("dotnet run --project tools/spielablauf -v q --nologo -- neubauten leiste turm bauen weiterbauen "
          "zeitalter fahne linksklick farm bewegen dunkel")

L1 = """Aufgabe L1: Die Kernregeln für die übrigen Gebäude aus Buildings.cs - Kosten, Bauzeit, Grundfläche und Werte. Die Verträge stehen als XML-Kommentar über den Methoden (VERTRAG), die Tests dazu in tests/AoE.Tests/ConstructionTests.cs (nicht ändern). Alles mit replace_in_file. Kommentare deutsch. Nach dem letzten Schritt sofort finish.

1. src/AoE.Core/Economy/Construction.cs, BuildingRules.CostOf: der switch kennt bisher nur die Gebäude bis zum Wachturm. Ergänze Arme genau nach dem Kommentar über der Methode: Schießstand (ArcheryRange), Stall (Stable), Markt (Market) und Kloster (Monastery) kosten wie die Kaserne 175 Holz - fasse sie mit ihr in einem Arm mit `or` zusammen, so wie Mill/LumberCamp/MiningCamp; Schmiede (Blacksmith) 150 Holz; SiegeWorkshop und University je 200 Holz; Castle 650 Stein; PalisadeWall 2 Holz; StoneWall 5 Stein; Wonder je 1000 Holz, Stein und Gold. Jeder Arm erzeugt ein neues Dictionary<Resource, int>. Der Arm `_ => new Dictionary<Resource, int>()` bleibt am Ende.

2. Dieselbe Datei, BuildingRules.BuildSecondsOf: Arme laut Kommentar ergänzen (PalisadeWall 5, StoneWall 8, Blacksmith/Monastery/SiegeWorkshop 40, ArcheryRange/Stable wie Barracks 50, Market/University 60, Castle 200, Wonder 3500; jeweils als float mit f). `_ => 60f` bleibt.

3. Dieselbe Datei, BuildingRules.SizeOf: Arme laut Kommentar (PalisadeWall/StoneWall 1; ArcheryRange, Stable, Blacksmith, Monastery wie Farm und Barracks 3; Market, SiegeWorkshop, University, Castle wie TownCenter 4; Wonder 5). `_ => 2` bleibt.

4. src/AoE.Core/Entities/Buildings.cs, BuildingEntity.Create. Suchtext:
    public static BuildingEntity Create(BuildingType type, int ownerId, Position position)
    {
        throw new NotImplementedException();
    }
Umsetzung nach dem Vertrag darüber: für TownCenter, House, Mill, LumberCamp, MiningCamp, Barracks und Tower die vorhandene Create-Methode des Typs aufrufen und ihr Ergebnis zurückgeben. Für die elf übrigen Typen ein new UnitStats { HitPoints = ..., BaseAttack = ..., BaseArmor = ..., Range = ..., Speed = 0, VisionRange = ... } mit den Werten aus der Tabelle im Kommentar (BaseAttack und Range 0, außer bei Castle 11 und 8) und return new BuildingEntity(type, ownerId, position, stats). Für BuildingType.Farm und jeden anderen Wert: throw new ArgumentOutOfRangeException(nameof(type), type, "...") mit einem kurzen deutschen Grund."""

L2_KOPF = """Aufgabe L2: Die übrigen Gebäude kommen ins Spiel - unter die bisherigen Befehlstasten eine zweite Tastenreihe mit den Gebäuden, die das Zeitalter freischaltet, dazu ihre Bilder und Symbole. Die Bilder liegen schon im Content-Verzeichnis (Gebaeude/<zeitalter>/<name>_blau.png und _rot.png, Icons/<name>.png) und sind in AgeOfEvolutions.mgcb eingetragen; BuildingEntity.Create(BuildingType, int ownerId, Position) aus AoE.Core gibt es seit L1.

AgeOfEvolutions/AgeOfEvolutions.Core/Screens/RTSGameplayScreen.cs ist über 4600 Zeilen lang: NICHT ganz lesen - die Suchtexte unten stehen genau so darin, lies höchstens kurze Abschnitte um eine Stelle. Alles mit replace_in_file. Kommentare deutsch. Gezeichnete Strings im Spielbildschirm nur mit Zeichen 32 bis 254 (also kein „–" und kein „…"); ä, ö, ü, ß sind erlaubt. Nach dem letzten Schritt sofort finish.

Die neuen Gebäude, in dieser Reihenfolge (Taste, BuildingType, Name auf der Karte, Bild, Symbol):
    K  Barracks       "Kaserne"               Gebaeude/kaserne               Icons/kaserne
    P  PalisadeWall   "Palisadenmauer"        Gebaeude/palisade              Icons/palisade
    S  ArcheryRange   "Schießstand"           Gebaeude/schiessstand          Icons/schiessstand
    L  Stable         "Stall"                 Gebaeude/stall                 Icons/stall
    E  Blacksmith     "Schmiede"              Gebaeude/schmiede              Icons/schmiede
    R  Market         "Markt"                 Gebaeude/markt                 Icons/markt
    W  StoneWall      "Steinmauer"            Gebaeude/steinmauer            Icons/steinmauer
    Z  TownCenter     "Stadtzentrum"          (hat schon ein Bild)           Icons/stadtzentrum
    X  SiegeWorkshop  "Belagerungswerkstatt"  Gebaeude/belagerungswerkstatt  Icons/belagerungswerkstatt
    U  University     "Universität"           Gebaeude/universitaet          Icons/universitaet
    O  Monastery      "Kloster"               Gebaeude/kloster               Icons/kloster
    C  Castle         "Burg"                  Gebaeude/burg                  Icons/burg
    N  Wonder         "Wunder"                Gebaeude/wunder                Icons/wunder
Welches Zeitalter ein Gebäude braucht, steht schon in AgeRules.RequiredAgeOf - nichts davon im Spielbildschirm nachbauen.

1. AgeOfEvolutions/AgeOfEvolutions.Core/Data/CoreBuildings.cs, Methode Create (kurze Datei, darf gelesen werden): nach der Zeile
        "Wachturm" => BuildingEntity.CreateTower(ownerId, center),
für die elf Namen aus dem Kommentar über der Methode (Schießstand bis Wunder; Kaserne und Stadtzentrum gibt es schon) je einen Arm "Name" => BuildingEntity.Create(BuildingType.X, ownerId, center), anlegen.

2. RTSGameplayScreen.cs, Feld BuildMenu. Suchtext:
    private static readonly (Keys Key, BuildingType Type, string Name)[] BuildMenu =
Das Tupel bekommt ein viertes Element `int Row`: 0 = erste Tastenreihe (die sechs bisherigen Einträge Haus bis Wachturm, unverändert in Reihenfolge und Kommentar), 1 = zweite Reihe darunter (die dreizehn neuen in der Reihenfolge der Tabelle). Darüber ein kurzer Kommentar, was Row bedeutet. Die übrigen Verwendungen von BuildMenu (e.Key, e.Type, e.Name, entry.Type ...) lesen die Elemente über ihre Namen und bleiben gültig.

3. Feld ButtonIcons. Suchtext:
        ("G", "Icons/farm"), ("T", "Icons/wachturm"), (".", "Icons/untaetig"),
Danach die dreizehn Paare ("Taste", "Symbol") aus der Tabelle ergänzen.

4. Feld BuildingSprites. Suchtext:
        ("Bergbaulager", "Gebaeude/bergbaulager"), ("Wachturm", "Gebaeude/wachturm"),
Danach die zwölf Paare ("Name", "Bild") aus der Tabelle ergänzen (ohne Stadtzentrum, das steht schon da).

5. Feld FlagCloth (Fahnentuch je Bild, am Bild gemessen). Suchtext:
        ["Gebaeude/imperial/wachturm"] = new(131, 7, 56, 37),
Danach genau diese Einträge ergänzen:
{fahnen}

6. Methode HandleRtsInput. Suchtext (zwei Zeilen):
        // Baumenü: H Haus, M Mühle, F Holzfällerlager, B Bergbaulager, G Farm,
        // ab der Feudalzeit T Wachturm.
Den Kommentar ergänzen: die Gebäude der zweiten Reihe ebenso mit dem Buchstaben ihrer Taste (BuildMenu). Am Code darunter ändert sich nichts.

7. Methode PlaceBuildingFor. Suchtext (zwei Zeilen):
        if (ownerId == 0)
            placing = null;   // Setzmodus nur für die menschliche Spielweise
Neu: nach einem Mauerstück (BuildingType.PalisadeWall oder BuildingType.StoneWall) bleibt der Setzmodus an, damit der Spieler mit weiteren Klicks eine Mauerreihe legt; nach jedem anderen Gebäude wird er wie bisher ausgeschaltet. Mit Kommentar.

8. Methode LayoutButtons. Suchtext:
        if (builders)
        {
            foreach (var e in BuildMenu.Where(e => AgeRules.IsUnlocked(e.Type, age)))
Die Schleife darunter legt die Bautasten in die erste Reihe; sie nimmt künftig nur Einträge mit Row 0. Nach der Taste "." (AddButton ... "Untätig" ... SelectNextIdleVillager) kommt die zweite Reihe: nur wenn builders, direkt unter der ersten (y + size + gap), links bündig bei x = 10, mit eigener Laufvariable für x, gleiche size und gap, mit den freigeschalteten Einträgen mit Row 1 - derselbe AddButton-Aufruf wie in der ersten Reihe (Taste, Name, CostText(BuildingRules.CostOf(e.Type)), e.Type, () => TogglePlacing(e.Type)). Mit Kommentar.

9. Methode DrawUI. Suchtext (eine Zeile):
        int infoLeft = _buttons[^1].Rect.Right + 24;
Die Einheiteninfo beginnt rechts neben der breitesten Tastenreihe: das größte Rect.Right über alle _buttons statt der letzten Taste (die letzte Taste liegt jetzt in der zweiten Reihe, die erste kann breiter sein). Mit Kommentar.

10. Methode DrawUI, Hilfetext. Suchtext:
Q/H/M/F/B/G/T: Befehle oben
Ersetzen durch:
Buchstaben: Befehle links unten"""

fahnen = json.loads((HIER / "fahnen.json").read_text(encoding="utf-8"))
zeilen = "\n".join(f'        ["{k}"] = new({x}, {y}, {b}, {h}),' for k, (x, y, b, h) in fahnen.items())

eintraege = [
    {"id": "L1", "title": "Kernregeln der übrigen Gebäude (Kosten, Bauzeit, Größe, Werte)", "auto": False,
     "max_turns": 25, "prompt": L1, "context_files": [],
     "allow_write": ["src/AoE.Core/Economy/Construction.cs", "src/AoE.Core/Entities/Buildings.cs"],
     "verify": [BUILD_GL, TEST]},
    {"id": "L2", "title": "Zweite Tastenreihe mit den übrigen Gebäuden, Bilder und Symbole", "auto": False,
     "max_turns": 40, "prompt": L2_KOPF.replace("{fahnen}", zeilen), "context_files": [],
     "allow_write": ["AgeOfEvolutions/AgeOfEvolutions.Core/Data/CoreBuildings.cs", SCREEN],
     "verify": [BUILD_GL, BUILD_DX, TEST, ABLAUF,
                "py tools/ollama-agent/checks/l2_neubauten.py",
                "py tools/ollama-agent/checks/c6a_bewegt.py",
                "py tools/ollama-agent/checks/start_rts.py gl",
                "py tools/ollama-agent/checks/start_rts.py dx"]},
]

pfad = ROOT / "tools/ollama-agent/tasks.json"
daten = json.loads(pfad.read_text(encoding="utf-8"))
ids = {e["id"] for e in eintraege}
daten["tasks"] = [t for t in daten["tasks"] if t["id"] not in ids] + eintraege
pfad.write_bytes((json.dumps(daten, ensure_ascii=False, indent=2) + "\n").encode("utf-8"))
print("eingetragen:", ", ".join(sorted(ids)), f"({len(fahnen)} Fahnentücher)")
