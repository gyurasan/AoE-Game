using System;
using System.Collections.Generic;
using System.Linq;
using AoE.Core.Ai;
using AoE.Core.Combat;
using AoE.Core.Entities;
using Microsoft.Xna.Framework;
using AgeOfEvolutions.Core.Data;
using AgeOfEvolutions.Core.Screens;
using Age = AoE.Core.Economy.Age;

namespace AgeOfEvolutions.Core.AI;

/// <summary>
/// Brücke von der AI-Schnittstelle (<see cref="IWorldState"/>,
/// <see cref="IWorldActions"/> aus AoE.Core) auf den <see cref="RTSGameplayScreen"/>.
///
/// Sie übersetzt AI-Befehle in die owner-generic Screen-Methoden und stellt
/// den Weltzustand als Snapshots bereit. Die Brücke hält keine eigene Logik —
/// nur Übersetzung.
///
/// WICHTIG: Die Bridge wird IM SPIEL-FADEN aufgerufen (die eingebaute KI tickt
/// aus <c>Update()</c>). Sie ruft die Screen-Methoden direkt, so dass die
/// Wirkung sofort sichtbar ist. Für externe Steuerungen aus anderen Fäden
/// (REST-API) steht <see cref="RTSGameplayScreen.EnqueueOrder"/> bereit; das
/// ruft dann dieselben Methoden nur im nächsten Frame.
/// </summary>
public sealed class AiBridge : IWorldState, IWorldActions
{
    private readonly RTSGameplayScreen _screen;

    public AiBridge(RTSGameplayScreen screen)
        => _screen = screen ?? throw new ArgumentNullException(nameof(screen));

    /// <summary>Der Spieler, den die Brücke repräsentiert. Standard: 1 (KI/Gegner).</summary>
    public int Owner { get; set; } = 1;

    /// <summary>Optionales Debug-Log (vom Agenten gesetzt); Standard: null.</summary>
    public Action<string>? DebugLog { get; set; }

    // ------------------- IWorldState -------------------

    public int Width => _screen.tileMap.Width;
    public int Height => _screen.tileMap.Height;

    private Player OwnerPlayer => Owner == 0 ? _screen.player1 : _screen.player2;
    // Schiffe führt die KI (noch) nicht wie Soldaten - sie fahren nur auf Wasser
    private IEnumerable<Unit> OwnUnits => _screen.units.Where(u => u.OwnerId == Owner && !u.IsShip);
    private IEnumerable<Building> OwnBuildings => _screen.tileMap.Buildings.Where(b => b.OwnerId == Owner);

    public ResourceVector Resources
    {
        get
        {
            var pool = OwnerPlayer.Resources;
            return new ResourceVector(
                pool[Resource.Food],
                pool[Resource.Wood],
                pool[Resource.Gold],
                pool[Resource.Stone]);
        }
    }

    public Age Age => OwnerPlayer.Ages.Current;
    public Age? AgeTarget => OwnerPlayer.Ages.Target;
    public float AgeProgress => OwnerPlayer.Ages.Progress;
    public int PopulationCount => OwnerPlayer.PopulationCount;
    public int PopulationCapacity => OwnerPlayer.PopulationLimit;

    public int VillagerTrainingCount
    {
        get
        {
            var b = OwnBuildings.FirstOrDefault(x => x.Core.BuildingType == BuildingType.TownCenter);
            return b is null ? 0 : b.Training.Count;
        }
    }

    public BuildingSnapshot? TownCenter
        => OwnBuildings.FirstOrDefault(b => b.Core.BuildingType == BuildingType.TownCenter)
            is { } b ? ToBuilding(b) : null;

    // Wer an Bord eines eigenen Transportschiffs ist, steht nicht in _screen.units - für die KI
    // bleibt er da (an der Stelle des Schiffs, unterwegs), sonst hielte die Armeeführung die Welle
    // auf See für aufgerieben
    public IReadOnlyList<UnitSnapshot> Units
        => OwnUnits.Select(u => ToUnit(u)).Concat(Riders().Select(r => ToUnit(r.Rider, r.Ship))).ToList();

    /// <summary>Die Fahrgäste der eigenen Schiffe, je mit ihrem Schiff.</summary>
    private IEnumerable<(Unit Rider, Unit Ship)> Riders()
        => _screen.units.Where(s => s.OwnerId == Owner && s.IsShip && s.Passengers.Count > 0)
                        .SelectMany(s => s.Passengers.Select(p => (p, s)));

    /// <summary>
    /// Landmasse nach TileMap.LandRegionAround: eine Wasserkachel am Strand (Hafenfläche, ein Schiff
    /// beim Anlegen) zählt zur Insel daneben, offene See zu keiner (false).
    /// </summary>
    public bool SameLand(int ax, int ay, int bx, int by)
    {
        var map = _screen.tileMap;
        int a = map.LandRegionAround(ax, ay);
        return a != 0 && a == map.LandRegionAround(bx, by);
    }

    public IReadOnlyList<BuildingSnapshot> Buildings
        => OwnBuildings.Select(ToBuilding).ToList();

    public bool IsExplored(int x, int y) => _screen.tileMap.IsTileExplored(x, y, Owner);

    public bool CanPlace(BuildingType type, int x, int y, int size)
    {
        if (!_screen.CanPlace(Owner, type, new Vector2(x, y)))
            return false;
        // Auf einer fremden Insel erreichen die Dorfbewohner die Baustelle nicht (Wiking-Karte)
        if (TownCenter is { } tc && type != BuildingType.Dock)
            return _screen.tileMap.SameLand(x, y, tc.X + tc.Width / 2, tc.Y + tc.Height / 2);
        return true;
    }

    /// <summary>
    /// Ob Move und Attack auf eine andere Insel über den Fährdienst laufen (AiFerry). Nur der
    /// AiAgent schaltet ihn ein - er tickt ihn auch; eine Brücke der REST-Schnittstelle lebt nur
    /// für einen Aufruf.
    /// </summary>
    public bool UseFerry { get; set; }

    private AiFerry? _ferry;

    /// <summary>Der Fährdienst dieses Spielers (Hafen, Transportschiffe, Übersetzen).</summary>
    public AiFerry Ferry => _ferry ??= new AiFerry(_screen, Owner);

    // Liegt das Ziel auf einer anderen Insel, übernimmt der Fährdienst den Befehl; sonst fährt die
    // Einheit nicht (mehr) mit
    private bool Ferried(Unit u, int x, int y)
    {
        if (!UseFerry)
            return false;
        if (Ferry.NeedsFerry(u, x, y))
        {
            Ferry.Request(u, x, y);
            return true;
        }
        Ferry.Cancel(u);
        return false;
    }

    /// <summary>Begehbar nach dem Wissen des Owners (Nebel gilt als begehbar) - wie die
    /// Wegsuche der Laufbefehle (<see cref="TileMap.IsKnownWalkable"/>).</summary>
    public bool IsWalkable(int x, int y) => _screen.tileMap.IsKnownWalkable(x, y, Owner);

    public (int X, int Y)? FindSource(Resource resource, int fromX, int fromY, int maxDistance)
    {
        var pos = _screen.gatherWorld.FindNearestSource(
            new Position(fromX, fromY), resource, maxDistance);
        return pos is null ? null : (pos.Value.X, pos.Value.Y);
    }

    /// <summary>
    /// Die Erkundungs-Front des Spielers: eine Zielkachel je Hauptrichtung
    /// am Rand des Bereichs, den er schon einmal gesehen hat. Die KI schickt
    /// Einheiten dorthin — der Weg wird nur über bekannte Kacheln geplant,
    /// und jeder Schritt deckt neue auf.
    /// </summary>
    public IReadOnlyList<(int X, int Y)> ExploreTargets()
    {
        var tc = TownCenter;
        if (tc is null) return Array.Empty<(int, int)>();
        var home = (tc.X + tc.Width / 2, tc.Y + tc.Height / 2);
        return _screen.tileMap.FrontierTargets(Owner, home);
    }

    /// <summary>
    /// Alle Feinde, die der Spieler gerade mit eigenen Augen sieht —
    /// feindliche Einheiten und Gebäude. Die KI darf nur gegen was
    /// anschlagen, was sie tatsächlich sieht.
    /// </summary>
    public IReadOnlyList<EnemyInfo> VisibleEnemies()
    {
        var visible = new List<EnemyInfo>();
        foreach (var u in _screen.units)
        {
            if (u.OwnerId == Owner || u.State == AoE.Core.Entities.UnitState.Dead) continue;
            // Schiffe erreicht die Landarmee nicht - die KI jagt ihnen nicht nach
            if (u.IsShip) continue;
            var grid = _screen.tileMap.WorldToGrid(u.Position);
            int x = (int)grid.X, y = (int)grid.Y;
            if (!_screen.tileMap.IsTileVisible(x, y, Owner)) continue;
            // IsAttackingUs: die feindliche Einheit greift gerade etwas des
            // Owners an — ihr Gebäude- oder Einheiten-Ziel gehört dem Owner.
            // Damit erkennt die KI einen Angriff auch fern vom Stadtzentrum.
            bool attackingUs = (u.AttackTarget is { } at && at.OwnerId == Owner)
                             || (u.UnitTarget is { } ut && ut.OwnerId == Owner);
            visible.Add(new EnemyInfo(x, y, IsBuilding: false, Health: u.Health, IsAttackingUs: attackingUs,
                                      Kind: KindOf(u)));
        }
        foreach (var b in _screen.tileMap.Buildings)
        {
            if (b.OwnerId == Owner || b.Core.CurrentHp <= 0) continue;
            bool seen = false;
            for (int x = b.X; x < b.X + b.Width && !seen; x++)
                for (int y = b.Y; y < b.Y + b.Height && !seen; y++)
                    if (_screen.tileMap.IsTileVisible(x, y, Owner)) seen = true;
            if (!seen) continue;
            // IsArmed (F1): das fertige Gebäude schießt selbst zurück - die
            // Welle weicht solchen Gebäuden aus, solange es andere gibt.
            visible.Add(new EnemyInfo(b.X + b.Width / 2, b.Y + b.Height / 2,
                                       IsBuilding: true, Health: b.Health,
                                       IsTownCenter: b.Core.BuildingType == BuildingType.TownCenter,
                                       IsArmed: b.IsComplete && BuildingCombat.Shoots(b.Core)));
        }
        return visible;
    }

    /// <summary>
    /// Wann hat die KI zuletzt Schaden genommen — in der selben Zeiteinheit,
    /// mit der die KI tickt (Sekunden seit Spielstart). Das Spiel führt die
    /// Uhr für beide Spieler; wenn noch nie getroffen: 0 (also „vor dem
    /// Spielbeginn").
    /// </summary>
    public double LastDamageAt => _screen.LastDamageReceivedAt(Owner);

    /// <summary>Weltzeit (Sekunden) — gleiche Skala wie
    /// <see cref="LastDamageAt"/>, damit die KI die Frische eines
    /// Treffers berechnen kann.</summary>
    public double WorldTime => _screen.GameSeconds;

    // Kurse am Markt (F8) - einer für alle Spieler, auf der Karte
    public int MarketBuyCost(Resource good) => _screen.tileMap.Market.BuyCost(good);
    public int MarketSellGain(Resource good) => _screen.tileMap.Market.SellGain(good);

    // ------------------- IWorldActions -------------------

    public void Gather(int unitId, int x, int y)
    {
        var u = UnitOf(unitId);
        if (u is null) return;
        // IssueCommand ist owner-generic: es prüft Nebel und Quelle aus Sicht
        // des Owners. Für ein Dorf (Quelle bekannt, sichtbar) wird es zu einem
        // Sammelauftrag; für einen Lauf ist es ein Bewegungsbefehl.
        _screen.IssueCommand(Owner, new List<Unit> { u }, new Vector2(x, y));
    }

    public void Move(int unitId, int x, int y)
    {
        if (RiderOf(unitId) is { } rider)
        {
            // an Bord: nur das Ziel der Fahrt ändert sich (gilt beim nächsten Anlegen)
            if (UseFerry) Ferry.Request(rider, x, y);
            return;
        }
        var u = UnitOf(unitId);
        if (u is null) return;
        if (Ferried(u, x, y)) return;
        // Lauf = IssueCommand auf eine Kachel, die keine Quelle ist — der
        // Screen erkennt das selbst (tile.ResourceType ist null).
        _screen.IssueCommand(Owner, new List<Unit> { u }, new Vector2(x, y));
    }

    public int? Build(BuildingType type, int x, int y, int[] builderIds)
    {
        var builders = new List<Unit>();
        foreach (var id in builderIds ?? Array.Empty<int>())
        {
            if (UnitOf(id) is { } u) builders.Add(u);
        }

        if (!_screen.PlaceBuildingFor(Owner, type, new Vector2(x, y), builders))
        {
            DebugLog?.Invoke($"Build ABGELEHNT: {type} bei ({x},{y}), {builders.Count} Arbeiter");
            return null;
        }

        // Das neue Gebäude ist das letzte der Liste des Owners (AddBuilding
        // hängt hinten an).
        var newest = _screen.tileMap.Buildings.LastOrDefault(b => b.OwnerId == Owner);
        DebugLog?.Invoke($"Build OK: {type} bei ({x},{y}), {builders.Count} Arbeiter");
        return newest?.Id.GetHashCode();
    }

    public void TrainVillager() => _screen.TrainVillager(Owner);
    public void AdvanceAge() => _screen.AdvanceAge(Owner);

    public void AssignBuilder(int unitId, int buildingId)
    {
        var u = UnitOf(unitId);
        if (u is null) return;
        var b = OwnBuildings.FirstOrDefault(b => b.Id.GetHashCode() == buildingId);
        if (b is null) return;
        _screen.AssignBuilder(u, b);
    }

    /// <summary>
    /// Greift den Feind bei (<paramref name="x"/>, <paramref name="y"/>) an —
    /// im Idealfall ein sichtbares feindliches Gebäude an genau der Kachel.
    /// Findet sich dort kein Gebäude, das der Owner greifen kann, fällt der
    /// Befehl auf eine Bewegung zurück (IssueCommand-Verhalten).
    /// </summary>
    public void Attack(int unitId, int x, int y)
    {
        if (RiderOf(unitId) is { } rider)
        {
            if (UseFerry) Ferry.Request(rider, x, y);
            return;
        }
        var u = UnitOf(unitId);
        if (u is null) return;
        if (Ferried(u, x, y)) return;
        _screen.IssueCommand(Owner, new List<Unit> { u }, new Vector2(x, y));
        DebugLog?.Invoke($"Attack: Einheit {unitId} auf ({x},{y})");
    }

    /// <summary>
    /// Bildet einen Soldaten aus. Die Bridge übersetzt <paramref name="soldier"/>
    /// auf den Spiel-<see cref="AoE.Core.Entities.UnitType"/> und ruft die
    /// owner-generic Screen-Methode auf. Scheitert still, wenn das Gebäude
    /// nicht ausbildet, nicht fertig ist, forscht oder kein Zeitalter erreicht
    /// ist — der Aufrufer liest dann die Warteschlange weiter.
    /// </summary>
    public void TrainSoldier(BuildingType building, UnitKind soldier)
    {
        Data.UnitType? unitType = soldier switch
        {
            UnitKind.Militia  => Data.UnitType.Militia,
            UnitKind.Spearman => Data.UnitType.SpearMan,
            UnitKind.Archer   => Data.UnitType.Archer,
            UnitKind.Cavalry  => Data.UnitType.Scout,
            UnitKind.Catapult => Data.UnitType.Catapult,   // F15: Katapult aus der Belagerungswerkstatt
            _                 => null,
        };
        if (unitType is null) return;
        // Das Gebäude finden: eigenes, fertig, und es bildet genau diese
        // Einheit aus. Fehlt das, bleibt die KI untätig — die Screen-Prüfung
        // verwarf die Aktion sonst mit einer Meldung, die niemand liest.
        var b = OwnBuildings.FirstOrDefault(x => x.Core.BuildingType == building
                                                 && x.IsComplete);
        if (b is null) return;
        _screen.TrainSoldierForOwner(Owner, b, unitType.Value);
    }

    /// <summary>Ein Los am eigenen fertigen Markt handeln (F8); ohne Markt bleibt es still.</summary>
    public void Trade(Resource good, bool buy) => _screen.TradeForOwner(Owner, good, buy);

    // ------------------- Snapshots -------------------

    private Unit UnitOf(int unitId)
        => _screen.units.FirstOrDefault(u => u.OwnerId == Owner && u.Id.GetHashCode() == unitId);

    /// <summary>Der Fahrgast mit dieser Id an Bord eines eigenen Schiffs, oder null.</summary>
    private Unit? RiderOf(int unitId)
        => Riders().Select(r => r.Rider).FirstOrDefault(u => u.Id.GetHashCode() == unitId);

    /// <summary>Die Einheit aus Sicht der KI; an Bord (<paramref name="ship"/> nicht null) an der Stelle
    /// des Schiffs, unterwegs, ohne Auftrag.</summary>
    private UnitSnapshot ToUnit(Unit u, Unit? ship = null)
    {
        var grid = _screen.tileMap.WorldToGrid((ship ?? u).Position);
        if (ship != null)
        {
            return new UnitSnapshot
            {
                Id = u.Id.GetHashCode(),
                Owner = u.OwnerId,
                Kind = KindOf(u),
                X = (int)grid.X,
                Y = (int)grid.Y,
                State = UnitStateKind.Moving,
                Health = u.Health,
                MaxHealth = u.MaxHealth,
            };
        }

        (int X, int Y, Resource)? gathering = (u.Job is { } job)
            && (u.State == AoE.Core.Entities.UnitState.Gathering
                || u.State == AoE.Core.Entities.UnitState.Returning
                || u.State == AoE.Core.Entities.UnitState.Moving)
            ? (job.Source.X, job.Source.Y, job.Resource)
            : null;

        return new UnitSnapshot
        {
            Id = u.Id.GetHashCode(),
            Owner = u.OwnerId,
            Kind = KindOf(u),
            X = (int)grid.X,
            Y = (int)grid.Y,
            State = FromState(u.State),
            Gathering = gathering,
            BuildingId = u.BuildSite is { } s ? s.Id.GetHashCode() : null,
            Health = u.Health,
            MaxHealth = u.MaxHealth,
            HasAttack = u.AttackTarget is not null || u.UnitTarget is not null,
            AttacksBuilding = u.AttackTarget is not null && u.UnitTarget is null
        };
    }

    /// <summary>
    /// Die Art einer Einheit aus Sicht der KI, nach ihrer Kerneinheit: Dorfbewohner;
    /// Bogen- und Plänkler als Schützen (Archer); Späher, Ritter und Kamelreiter als
    /// Reiter (Cavalry); Speerkämpfer; Rammbock, Mangonel, Trebuchet als Belagerung
    /// (Catapult); alles andere (Miliz und ihre Linie, Mönch) als Nahkämpfer (Militia).
    /// Bis 2026-11-07 war jeder Soldat „Miliz" - die KI konnte Schützen nicht von
    /// Nahkämpfern unterscheiden.
    /// </summary>
    public int QueuedSoldiers(UnitKind kind)
        => OwnBuildings.Where(b => b.Core.BuildingType != BuildingType.TownCenter)
                       .Sum(b => b.Training.Units.Count(t => KindOf(t) == kind));

    /// <summary>Die KI-Art eines Einheitentyps aus der Warteschlange - dieselbe Einteilung wie
    /// <see cref="KindOf(Unit)"/>.</summary>
    internal static UnitKind KindOf(Data.UnitType t) => t switch
    {
        Data.UnitType.Villager => UnitKind.Villager,
        Data.UnitType.Archer or Data.UnitType.Skirmisher or Data.UnitType.Longbowman
            or Data.UnitType.CavalryArcher => UnitKind.Archer,
        Data.UnitType.Scout or Data.UnitType.Cavalry or Data.UnitType.CamelRider
            or Data.UnitType.Paladin or Data.UnitType.ImperialCamel => UnitKind.Cavalry,
        Data.UnitType.SpearMan => UnitKind.Spearman,
        Data.UnitType.Ram or Data.UnitType.Catapult or Data.UnitType.Trebuchet => UnitKind.Catapult,
        _ => UnitKind.Militia,
    };

    internal static UnitKind KindOf(Unit u) => u.Core switch
    {
        Villager => UnitKind.Villager,
        AoE.Core.Entities.Archer or Skirmisher => UnitKind.Archer,
        Scout or Knight or CamelRider => UnitKind.Cavalry,
        SpearMan => UnitKind.Spearman,
        Ram or Mangonel or Trebuchet => UnitKind.Catapult,
        _ => UnitKind.Militia,
    };

    private BuildingSnapshot ToBuilding(Building b)
    {
        int active = 0;
        foreach (var u in OwnUnits)
            if (u.BuildSite == b
                && (u.State == AoE.Core.Entities.UnitState.Building
                    || u.State == AoE.Core.Entities.UnitState.Moving))
                active++;

        return new BuildingSnapshot
        {
            Id = b.Id.GetHashCode(),
            Owner = b.OwnerId,
            Type = b.Core.BuildingType,
            X = b.X,
            Y = b.Y,
            Width = b.Width,
            Height = b.Height,
            IsComplete = b.IsComplete,
            ConstructionProgress = b.Construction?.Progress ?? 0f,
            ActiveBuilders = active,
            Health = b.Health,
            MaxHealth = b.MaxHealth
        };
    }

    private static UnitStateKind FromState(AoE.Core.Entities.UnitState s) => s switch
    {
        AoE.Core.Entities.UnitState.Idle      => UnitStateKind.Idle,
        AoE.Core.Entities.UnitState.Gathering => UnitStateKind.Gathering,
        AoE.Core.Entities.UnitState.Building  => UnitStateKind.Building,
        AoE.Core.Entities.UnitState.Moving    => UnitStateKind.Moving,
        AoE.Core.Entities.UnitState.Returning => UnitStateKind.Returning,
        AoE.Core.Entities.UnitState.Dead      => UnitStateKind.Dead,
        _                                     => UnitStateKind.Idle
    };
}
