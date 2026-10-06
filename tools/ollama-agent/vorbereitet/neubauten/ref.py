"""Referenzumsetzung für L1 und L2 - NUR in der Scratchpad-Kopie (spiegel/).

    py ref.py l1   # Kernregeln, BuildingEntity.Create
    py ref.py l2   # CoreBuildings, Baumenü, zweite Reihe, Bilder
"""
import sys
from pathlib import Path

K = Path(__file__).resolve().parent / "spiegel"
assert "scratchpad" in str(K)


def patch(rel, paare):
    p = K / rel
    t = p.read_bytes().decode("utf-8")
    bom = t.startswith("﻿")
    crlf = "\r\n" in t
    t = t.lstrip("﻿").replace("\r\n", "\n")
    for alt, neu in paare:
        assert t.count(alt) == 1, (rel, t.count(alt), alt[:80])
        t = t.replace(alt, neu)
    if crlf:
        t = t.replace("\n", "\r\n")
    p.write_bytes((("﻿" if bom else "") + t).encode("utf-8"))
    print("Referenz:", rel)


def l1():
    patch("src/AoE.Core/Economy/Construction.cs", [
        ("""        BuildingType.Barracks => new Dictionary<Resource, int> { [Resource.Wood] = 175 },
""",
         """        BuildingType.Barracks or BuildingType.ArcheryRange or BuildingType.Stable
            or BuildingType.Market or BuildingType.Monastery => new Dictionary<Resource, int> { [Resource.Wood] = 175 },
        BuildingType.Blacksmith => new Dictionary<Resource, int> { [Resource.Wood] = 150 },
        BuildingType.SiegeWorkshop or BuildingType.University => new Dictionary<Resource, int> { [Resource.Wood] = 200 },
        BuildingType.Castle => new Dictionary<Resource, int> { [Resource.Stone] = 650 },
        BuildingType.PalisadeWall => new Dictionary<Resource, int> { [Resource.Wood] = 2 },
        BuildingType.StoneWall => new Dictionary<Resource, int> { [Resource.Stone] = 5 },
        BuildingType.Wonder => new Dictionary<Resource, int> { [Resource.Wood] = 1000, [Resource.Stone] = 1000, [Resource.Gold] = 1000 },
"""),
        ("""        BuildingType.Barracks => 50f,
        BuildingType.Tower => 80f,
        BuildingType.TownCenter => 150f,
""",
         """        BuildingType.Barracks or BuildingType.ArcheryRange or BuildingType.Stable => 50f,
        BuildingType.Blacksmith or BuildingType.Monastery or BuildingType.SiegeWorkshop => 40f,
        BuildingType.Market or BuildingType.University => 60f,
        BuildingType.PalisadeWall => 5f,
        BuildingType.StoneWall => 8f,
        BuildingType.Tower => 80f,
        BuildingType.TownCenter => 150f,
        BuildingType.Castle => 200f,
        BuildingType.Wonder => 3500f,
"""),
        ("""        BuildingType.TownCenter => 4,
        BuildingType.Farm or BuildingType.Barracks => 3,
""",
         """        BuildingType.PalisadeWall or BuildingType.StoneWall => 1,
        BuildingType.TownCenter or BuildingType.Market or BuildingType.SiegeWorkshop
            or BuildingType.University or BuildingType.Castle => 4,
        BuildingType.Farm or BuildingType.Barracks or BuildingType.ArcheryRange or BuildingType.Stable
            or BuildingType.Blacksmith or BuildingType.Monastery => 3,
        BuildingType.Wonder => 5,
"""),
    ])
    patch("src/AoE.Core/Entities/Buildings.cs", [
        ("""    public static BuildingEntity Create(BuildingType type, int ownerId, Position position)
    {
        throw new NotImplementedException();
    }""",
         """    public static BuildingEntity Create(BuildingType type, int ownerId, Position position)
    {
        switch (type)
        {
            case BuildingType.TownCenter: return CreateTownCenter(ownerId, position);
            case BuildingType.House: return CreateHouse(ownerId, position);
            case BuildingType.Mill: return CreateMill(ownerId, position);
            case BuildingType.LumberCamp: return CreateLumberCamp(ownerId, position);
            case BuildingType.MiningCamp: return CreateMiningCamp(ownerId, position);
            case BuildingType.Barracks: return CreateBarracks(ownerId, position);
            case BuildingType.Tower: return CreateTower(ownerId, position);
        }
        (int hp, int armor, int vision, int attack, int range) = type switch
        {
            BuildingType.ArcheryRange or BuildingType.Stable => (1500, 1, 5, 0, 0),
            BuildingType.Blacksmith => (1800, 1, 5, 0, 0),
            BuildingType.Market or BuildingType.University or BuildingType.Monastery => (2100, 1, 6, 0, 0),
            BuildingType.SiegeWorkshop => (2100, 1, 5, 0, 0),
            BuildingType.Castle => (4800, 8, 11, 11, 8),
            BuildingType.PalisadeWall => (250, 2, 2, 0, 0),
            BuildingType.StoneWall => (1800, 8, 2, 0, 0),
            BuildingType.Wonder => (4800, 3, 8, 0, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "kein Gebäude")
        };
        var stats = new UnitStats
        {
            HitPoints = hp,
            BaseAttack = attack,
            BaseArmor = armor,
            Range = range,
            Speed = 0,
            VisionRange = vision
        };
        return new BuildingEntity(type, ownerId, position, stats);
    }"""),
    ])


def l2():
    patch("AgeOfEvolutions/AgeOfEvolutions.Core/Data/CoreBuildings.cs", [
        ("""        "Wachturm" => BuildingEntity.CreateTower(ownerId, center),
""",
         """        "Wachturm" => BuildingEntity.CreateTower(ownerId, center),
        "Schießstand" => BuildingEntity.Create(BuildingType.ArcheryRange, ownerId, center),
        "Stall" => BuildingEntity.Create(BuildingType.Stable, ownerId, center),
        "Schmiede" => BuildingEntity.Create(BuildingType.Blacksmith, ownerId, center),
        "Markt" => BuildingEntity.Create(BuildingType.Market, ownerId, center),
        "Palisadenmauer" => BuildingEntity.Create(BuildingType.PalisadeWall, ownerId, center),
        "Steinmauer" => BuildingEntity.Create(BuildingType.StoneWall, ownerId, center),
        "Belagerungswerkstatt" => BuildingEntity.Create(BuildingType.SiegeWorkshop, ownerId, center),
        "Universität" => BuildingEntity.Create(BuildingType.University, ownerId, center),
        "Kloster" => BuildingEntity.Create(BuildingType.Monastery, ownerId, center),
        "Burg" => BuildingEntity.Create(BuildingType.Castle, ownerId, center),
        "Wunder" => BuildingEntity.Create(BuildingType.Wonder, ownerId, center),
"""),
    ])
    patch("AgeOfEvolutions/AgeOfEvolutions.Core/Screens/RTSGameplayScreen.cs", [
        ("""        ("G", "Icons/farm"), ("T", "Icons/wachturm"), (".", "Icons/untaetig"),
    };""",
         """        ("G", "Icons/farm"), ("T", "Icons/wachturm"), (".", "Icons/untaetig"),
        ("K", "Icons/kaserne"), ("P", "Icons/palisade"), ("S", "Icons/schiessstand"),
        ("L", "Icons/stall"), ("E", "Icons/schmiede"), ("R", "Icons/markt"),
        ("W", "Icons/steinmauer"), ("Z", "Icons/stadtzentrum"), ("X", "Icons/belagerungswerkstatt"),
        ("U", "Icons/universitaet"), ("O", "Icons/kloster"), ("C", "Icons/burg"), ("N", "Icons/wunder"),
    };"""),
        ("""        ("Bergbaulager", "Gebaeude/bergbaulager"), ("Wachturm", "Gebaeude/wachturm"),
    };""",
         """        ("Bergbaulager", "Gebaeude/bergbaulager"), ("Wachturm", "Gebaeude/wachturm"),
        ("Kaserne", "Gebaeude/kaserne"), ("Palisadenmauer", "Gebaeude/palisade"),
        ("Schießstand", "Gebaeude/schiessstand"), ("Stall", "Gebaeude/stall"),
        ("Schmiede", "Gebaeude/schmiede"), ("Markt", "Gebaeude/markt"),
        ("Steinmauer", "Gebaeude/steinmauer"), ("Belagerungswerkstatt", "Gebaeude/belagerungswerkstatt"),
        ("Universität", "Gebaeude/universitaet"), ("Kloster", "Gebaeude/kloster"),
        ("Burg", "Gebaeude/burg"), ("Wunder", "Gebaeude/wunder"),
    };"""),
        ("""    private static readonly (Keys Key, BuildingType Type, string Name)[] BuildMenu =
    {
        (Keys.H, BuildingType.House, "Haus"),
        (Keys.M, BuildingType.Mill, "Mühle"),
        (Keys.F, BuildingType.LumberCamp, "Holzfällerlager"),
        (Keys.B, BuildingType.MiningCamp, "Bergbaulager"),
        (Keys.G, BuildingType.Farm, "Farm"),
        (Keys.T, BuildingType.Tower, "Wachturm"),   // ab der Feudalzeit
    };""",
         """    // Row 0 ist die erste Tastenreihe, Row 1 die zweite darunter.
    private static readonly (Keys Key, BuildingType Type, string Name, int Row)[] BuildMenu =
    {
        (Keys.H, BuildingType.House, "Haus", 0),
        (Keys.M, BuildingType.Mill, "Mühle", 0),
        (Keys.F, BuildingType.LumberCamp, "Holzfällerlager", 0),
        (Keys.B, BuildingType.MiningCamp, "Bergbaulager", 0),
        (Keys.G, BuildingType.Farm, "Farm", 0),
        (Keys.T, BuildingType.Tower, "Wachturm", 0),   // ab der Feudalzeit
        (Keys.K, BuildingType.Barracks, "Kaserne", 1),
        (Keys.P, BuildingType.PalisadeWall, "Palisadenmauer", 1),
        (Keys.S, BuildingType.ArcheryRange, "Schießstand", 1),   // ab der Feudalzeit
        (Keys.L, BuildingType.Stable, "Stall", 1),
        (Keys.E, BuildingType.Blacksmith, "Schmiede", 1),
        (Keys.R, BuildingType.Market, "Markt", 1),
        (Keys.W, BuildingType.StoneWall, "Steinmauer", 1),
        (Keys.Z, BuildingType.TownCenter, "Stadtzentrum", 1),   // ab der Ritterzeit
        (Keys.X, BuildingType.SiegeWorkshop, "Belagerungswerkstatt", 1),
        (Keys.U, BuildingType.University, "Universität", 1),
        (Keys.O, BuildingType.Monastery, "Kloster", 1),
        (Keys.C, BuildingType.Castle, "Burg", 1),
        (Keys.N, BuildingType.Wonder, "Wunder", 1),   // Imperialzeit
    };"""),
        ("""        // Baumenü: H Haus, M Mühle, F Holzfällerlager, B Bergbaulager, G Farm,
        // ab der Feudalzeit T Wachturm.
""",
         """        // Baumenü: H Haus, M Mühle, F Holzfällerlager, B Bergbaulager, G Farm,
        // ab der Feudalzeit T Wachturm; die Gebäude der zweiten Reihe ebenso mit
        // dem Buchstaben ihrer Taste (BuildMenu).
"""),
        ("""        if (ownerId == 0)
            placing = null;   // Setzmodus nur für die menschliche Spielweise
""",
         """        // Setzmodus nur für die menschliche Spielweise. Nach einem Mauerstück
        // bleibt er an - so legt man mit mehreren Klicks eine Mauerreihe
        if (ownerId == 0 && type is not (BuildingType.PalisadeWall or BuildingType.StoneWall))
            placing = null;
"""),
        ("""        if (builders)
        {
            foreach (var e in BuildMenu.Where(e => AgeRules.IsUnlocked(e.Type, age)))
                AddButton(ref x, size, gap, y, e.Key.ToString().ToUpperInvariant(),
                          e.Name, CostText(BuildingRules.CostOf(e.Type)), e.Type,
                          () => TogglePlacing(e.Type));
        }
        AddButton(ref x, size, gap, y, ".", "Untätig", "nächster untätiger Dorfbewohner",
                  SelectNextIdleVillager);
""",
         """        if (builders)
        {
            foreach (var e in BuildMenu.Where(e => e.Row == 0 && AgeRules.IsUnlocked(e.Type, age)))
                AddButton(ref x, size, gap, y, e.Key.ToString().ToUpperInvariant(),
                          e.Name, CostText(BuildingRules.CostOf(e.Type)), e.Type,
                          () => TogglePlacing(e.Type));
        }
        AddButton(ref x, size, gap, y, ".", "Untätig", "nächster untätiger Dorfbewohner",
                  SelectNextIdleVillager);

        // Zweite Reihe darunter: die übrigen Gebäude des Zeitalters
        if (builders)
        {
            int x2 = 10, y2 = y + size + gap;
            foreach (var e in BuildMenu.Where(e => e.Row == 1 && AgeRules.IsUnlocked(e.Type, age)))
                AddButton(ref x2, size, gap, y2, e.Key.ToString().ToUpperInvariant(),
                          e.Name, CostText(BuildingRules.CostOf(e.Type)), e.Type,
                          () => TogglePlacing(e.Type));
        }
"""),
        ("""   Q/H/M/F/B/G/T: Befehle oben   A""",
         """   Buchstaben: Befehle links unten   A"""),
        ("""        int infoLeft = _buttons[^1].Rect.Right + 24;""",
         """        int infoLeft = _buttons.Max(b => b.Rect.Right) + 24;"""),
    ])


{"l1": l1, "l2": l2}[sys.argv[1]]()
