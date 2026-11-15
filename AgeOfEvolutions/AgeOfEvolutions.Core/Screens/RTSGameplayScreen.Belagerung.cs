using System;
using System.Collections.Generic;
using System.Linq;
using AgeOfEvolutions.Core.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Resource = AoE.Core.Entities.Resource;
using UnitState = AoE.Core.Entities.UnitState;
using CoreVillager = AoE.Core.Entities.Villager;
using BuildingRules = AoE.Core.Economy.BuildingRules;

namespace AgeOfEvolutions.Core.Screens;

// Belagerungswaffen im Bild (F15; Nutzer 2026-11-08: „die Belagerungs Waffen werden noch nicht
// Dargestellt im game“). Rammbock, Katapult (Mangonel) und Tribok kommen aus tools/bilder: das
// Standbild (Gruppe belagerung), der abgebaute Tribok für die Fahrt und je acht Angriffsphasen
// (Gruppe teilanim, tools/bilder/teilanim.py) - auf einer größeren Leinwand im Maßstab des
// Standbilds, die Räder an derselben Stelle. Die Geräte rollen, statt zu wippen; Katapult und
// Tribok werfen Steine statt Pfeile (RTSGameplayScreen.Animationen, Arrow.Stone).
public partial class RTSGameplayScreen
{
    /// <summary>
    /// Bilder der Belagerungswaffen (_blau und _rot) und ihre Breite in Welteinheiten: ein
    /// Dorfbewohner ist 24 hoch, eine Kachel 32 breit. Aspect ist Höhe durch Breite des
    /// Standbilds - für die Trefferfläche, die ohne geladenes Bild auskommen muss.
    /// </summary>
    private static readonly (UnitType Unit, string Asset, float Length, float Aspect)[] SiegeSprites =
    {
        (UnitType.Ram, "Einheiten/rammbock", 44f, 1.04f),
        (UnitType.Catapult, "Einheiten/mangonel", 44f, 1.06f),
        (UnitType.Trebuchet, "Einheiten/trebuchet", 72f, 1.06f),
    };

    /// <summary>Breite des abgebauten Tribok (Karren mit Balken) in Welteinheiten.</summary>
    private const float TREBUCHET_PACKED_LENGTH = 68f;

    /// <summary>Angriffsphasen je Belagerungswaffe (teilanim.py).</summary>
    private const int SIEGE_FRAMES = 8;

    private readonly Dictionary<(UnitType Unit, int Owner), Texture2D> _siegeSprites = new();
    private readonly Dictionary<(UnitType Unit, int Owner), Texture2D[]> _siegeAttack = new();
    private readonly Dictionary<int, Texture2D> _trebuchetPacked = new();

    // Steine von Katapult und Tribok (tools/bilder, Gruppe geschosse)
    private Texture2D _stoneSmall, _stoneLarge;

    /// <summary>
    /// Welches Angriffsbild der Rammbock zeigt, nach dem Anteil d des Schlagtakts seit dem Stoß:
    /// Phase 5 ist der Balken ganz vorn (Treffer bei d = 0), 6 und 7 federn zurück, 0 ruht, 1 bis 3
    /// holen aus, 4 stößt vor.
    /// </summary>
    private static readonly (float From, int Frame)[] RamTimeline =
    {
        (0f, 5), (0.04f, 6), (0.10f, 7), (0.18f, 0), (0.55f, 1), (0.68f, 2), (0.80f, 3), (0.90f, 4), (0.95f, 5),
    };

    /// <summary>
    /// Welches Angriffsbild Katapult und Tribok zeigen, nach dem Anteil d des Schlagtakts seit dem
    /// Loslassen des Steins: Phase 4 ist der Arm ganz oben (der Stein fliegt bei d = 0), 5 bis 7
    /// kehrt er zurück, 0 ist gespannt, 1 bis 3 schnellt er hoch.
    /// </summary>
    private static readonly (float From, int Frame)[] ThrowTimeline =
    {
        (0f, 4), (0.05f, 5), (0.12f, 6), (0.22f, 7), (0.32f, 0), (0.78f, 1), (0.92f, 2), (0.96f, 3),
    };

    /// <summary>Wurfpunkt in Breiten des Standbilds, vom Fußpunkt aus, das Gerät nach rechts
    /// gewandt: der Becher des Katapults und die Schlinge des Tribok im höchsten Bild (Phase 4,
    /// Datei *_angriff5), am Bild gemessen.</summary>
    private static Vector2 StoneLaunchOffset(UnitType type) => type == UnitType.Trebuchet
        ? new Vector2(-0.16f, -1.32f)
        : new Vector2(0.14f, -1.07f);

    internal static bool IsSiegeType(UnitType type)
        => type is UnitType.Ram or UnitType.Catapult or UnitType.Trebuchet;

    /// <summary>Leistenmeldung (F15): Tribok auf einen Einheitenbefehl gesetzt.</summary>
    internal const string TRIBOK_BUILDINGS_ONLY = "Der Tribok wirft nur gegen Gebäude";

    /// <summary>
    /// Tribok-Aufbau je Bild (F15): der Timer läuft ab, und am Ende wird aus der
    /// Absicht (<see cref="Unit.Deploying"/>) der Zustand (<see cref="Unit.Deployed"/>).
    /// Wird für alle Einheiten gerufen — Nicht-Triboks und Geräte ohne laufenden
    /// Auf-/Abbau gehen sofort zurück.
    /// </summary>
    private static void UpdateSiegeDeploy(Unit unit, float dt)
    {
        if (unit.Type != UnitType.Trebuchet || unit.DeployTimer <= 0f)
            return;
        unit.DeployTimer -= dt;
        if (unit.DeployTimer > 0f)
            return;
        unit.DeployTimer = 0f;
        unit.Deployed = unit.Deploying;   // der Aufbau ist fertig, der Abbau vorbei
        unit.Deploying = false;
    }

    /// <summary>
    /// Triboks der eigenen Auswahl auf- bzw. abbauen (Taste S, F15): steht der
    /// Balken, wird abgebaut (danach fahrbar); steht er nicht, aufgebaut
    /// (danach feuerfest, aber unbeweglich). Ein laufender Aufbau kippt zum
    /// Abbau und umgekehrt. Gibt zurück, ob überhaupt ein Tribok ausgewählt war
    /// — nur dann gilt S als verbraucht und schaltet keinen Schießstand-Setzmodus.
    /// </summary>
    private bool TryToggleSiegeDeploy()
    {
        var devices = new List<Unit>();
        foreach (var u in selectedUnits)
            if (u.OwnerId == 0 && u.Type == UnitType.Trebuchet)
                devices.Add(u);
        if (devices.Count == 0)
            return false;

        foreach (var d in devices)
        {
            if (d.Deployed || d.Deploying)
            {
                d.Deployed = false;
                d.Deploying = false;                                  // Abbau, auch aus dem Aufbau
                d.DeployTimer = AoE.Core.Combat.SiegeRules.PACK_SECONDS;
            }
            else
            {
                d.Deploying = true;
                d.DeployTimer = AoE.Core.Combat.SiegeRules.SETUP_SECONDS;
            }
        }
        ShowHudMessage(devices[0].Deploying ? "Der Tribok baut auf" : "Der Tribok fährt zusammen");
        return true;
    }

    /// <summary>Welchen Stein eine Einheit wirft: 1 das Katapult, 2 der Tribok (der große), sonst 0
    /// (Pfeil oder gar nichts).</summary>
    internal static int StoneOf(UnitType type) => type switch
    {
        UnitType.Catapult => 1,
        UnitType.Trebuchet => 2,
        _ => 0,
    };

    private static float SiegeLength(UnitType type)
    {
        foreach (var (unit, _, length, _) in SiegeSprites)
            if (unit == type) return length;
        return 44f;
    }

    private static float SiegeAspect(UnitType type)
    {
        foreach (var (unit, _, _, aspect) in SiegeSprites)
            if (unit == type) return aspect;
        return 1f;
    }

    /// <summary>Trefferfläche einer Belagerungswaffe in Weltkoordinaten: so breit und hoch wie ihr
    /// Standbild, waagrecht mittig über der Position, Unterkante (die Räder) auf ihr.</summary>
    private static Rectangle SiegeWorldRect(Unit unit)
    {
        float width = SiegeLength(unit.Type);
        float height = width * SiegeAspect(unit.Type);
        return new Rectangle((int)(unit.Position.X - width / 2f), (int)(unit.Position.Y - height),
                             (int)width, (int)height);
    }

    /// <summary>Wo der Stein das Gerät verlässt, wenn es auf <paramref name="to"/> wirft.</summary>
    private static Vector2 StoneLaunchPoint(Unit unit, Vector2 to)
    {
        var offset = StoneLaunchOffset(unit.Type) * SiegeLength(unit.Type);
        if (to.X < unit.Position.X)
            offset.X = -offset.X;
        return unit.Position + offset;
    }

    /// <summary>
    /// Lädt Stand-, Fahr- und Angriffsbilder der Belagerungswaffen je Spielerfarbe und die Steine.
    /// Fehlt das Standbild, zeichnet DrawUnits das Gerät wie bisher als Quadrat; fehlt eine der
    /// acht Angriffsphasen, bleibt es beim Angriff im Standbild.
    /// </summary>
    private void LoadSiegeSprites()
    {
        foreach (var (owner, colour) in new[] { (0, "_blau"), (1, "_rot") })
        {
            foreach (var (unit, asset, _, _) in SiegeSprites)
            {
                if (LoadOptional(asset + colour) is not { } stand)
                    continue;
                _siegeSprites[(unit, owner)] = stand;
                var frames = new Texture2D[SIEGE_FRAMES];
                bool all = true;
                for (int i = 0; i < SIEGE_FRAMES && all; i++)
                    all = (frames[i] = LoadOptional($"{asset}_angriff{i + 1}{colour}")) != null;
                if (all)
                    _siegeAttack[(unit, owner)] = frames;
            }
            if (LoadOptional("Einheiten/trebuchet_abgebaut" + colour) is { } packed)
                _trebuchetPacked[owner] = packed;
        }
        _stoneSmall = LoadOptional("Effekte/stein");
        _stoneLarge = LoadOptional("Effekte/stein_gross");
    }

    /// <summary>
    /// Das Angriffsbild (0 bis 7) einer Belagerungswaffe im Schlagtakt. Der Rammbock stößt genau,
    /// wenn AttackTimer BuildingCombat.RELOAD_SECONDS erreicht (der Treffer); Katapult und Tribok
    /// lassen den Stein früher los - so viel früher, wie er fliegt (MaybeLaunchArrow) -, und der
    /// Arm schnellt genau dann hoch.
    /// </summary>
    internal int SiegeAttackFrame(Unit unit)
    {
        float reload = AoE.Core.Combat.BuildingCombat.RELOAD_SECONDS;
        float f = Math.Clamp(unit.AttackTimer / reload, 0f, 0.9999f);
        // Wann im Takt der Stein losfliegt (Anteil); der Rammbock stößt am Ende des Takts
        float release = 1f;
        if (StoneOf(unit.Type) > 0)
        {
            Vector2 to = ArrowTarget(unit);
            release = 1f - StoneFlight(Vector2.Distance(StoneLaunchPoint(unit, to), to)) / reload;
        }
        // Anteil des Takts seit dem Loslassen bzw. dem Stoß
        float d = f - release;
        if (d < 0f)
            d += 1f;
        var timeline = unit.Type == UnitType.Ram ? RamTimeline : ThrowTimeline;
        int frame = timeline[0].Frame;
        foreach (var (from, fr) in timeline)
        {
            if (from <= d)
                frame = fr;
            else
                break;
        }
        return frame;
    }

    /// <summary>
    /// Eine Belagerungswaffe in der Tiefenschicht: das Standbild so breit wie SiegeLength, die Räder
    /// auf screenPos, nach links gespiegelt, wenn sie nach links fährt oder zielt. Beim Fahren
    /// rumpelt sie leicht nach der gefahrenen Strecke, der Tribok fährt abgebaut; beim Angriff zeigt
    /// sie die Angriffsphase im Takt (SiegeAttackFrame) im Maßstab des Standbilds.
    /// </summary>
    private void DrawSiege(SpriteBatch spriteBatch, Unit unit, Texture2D stand, Vector2 screenPos, bool hover)
    {
        var motion = _unitMotion.GetValueOrDefault(unit);
        bool moving = motion.MovingFor > 0f;
        var flip = motion.FacingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        float scale = SiegeLength(unit.Type) * cameraZoom / stand.Width;
        Texture2D figure = stand;
        float bob = 0f, tilt = 0f;

        // F15: Der Tribok zeigt das Karrenbild, solange er NICHT aufgebaut ist —
        // im Fahren ebenso wie im Stehen (beim Auf-/Abbau). Aufgebaut steht und
        // wirft er im vollen Bild.
        if (unit.Type == UnitType.Trebuchet && !unit.Deployed
            && _trebuchetPacked.TryGetValue(unit.OwnerId, out var packedIdle))
        {
            figure = packedIdle;
            scale = TREBUCHET_PACKED_LENGTH * cameraZoom / packedIdle.Width;
        }

        if (moving)
        {
            // Holzräder auf unebenem Boden: ein leichtes Rumpeln nach der Strecke, nicht nach der Uhr
            bob = (1f - MathF.Cos(motion.Walked * 0.7f)) * 0.4f * cameraZoom;
            tilt = MathF.Sin(motion.Walked * 0.35f) * 0.012f;
        }
        else if (IsStriking(unit) && _siegeAttack.TryGetValue((unit.Type, unit.OwnerId), out var frames))
        {
            // Angriffsbilder: größere Leinwand im Maßstab des Standbilds, Unterkante gleich
            figure = frames[SiegeAttackFrame(unit)];
        }

        // Schlagschatten (C7) am Bodenkontakt - er rumpelt nicht mit
        int width = (int)(figure.Width * scale), height = (int)(figure.Height * scale);
        DrawCastShadow(spriteBatch, figure,
                       new Rectangle((int)screenPos.X - width / 2, (int)screenPos.Y - height, width, height),
                       flip, false);

        var origin = new Vector2(figure.Width / 2f, figure.Height);
        var at = screenPos - new Vector2(0f, bob);
        float rotation = motion.FacingLeft ? -tilt : tilt;
        spriteBatch.Draw(figure, at, null, Color.White, rotation, origin, scale, flip, 0f);
        // C17: hover - dasselbe Bild in derselben Lage noch einmal additiv
        if (hover)
            spriteBatch.Draw(figure, at, null, HoverLift, rotation, origin, scale, flip, 0f);
    }

    /// <summary>
    /// Ein fliegender Stein (Arrow mit Stone &gt; 0): das Steinbild auf der Wurfparabel, im Flug
    /// drehend, darunter ein flacher Schatten am Boden. Ohne Bild ein graues Quadrat.
    /// </summary>
    private void DrawStone(SpriteBatch spriteBatch, Arrow a)
    {
        Vector2 p = WorldToScreen(ArrowAt(a, a.Age, out _));
        float size = (a.Stone == 2 ? STONE_SIZE_LARGE : STONE_SIZE) * cameraZoom;

        // Schatten am Boden, unter dem Stein entlang der Flugstrecke
        float s = MathHelper.Clamp(a.Age / a.Flight, 0, 1);
        Vector2 ground = WorldToScreen(Vector2.Lerp(a.From, a.To, s) + new Vector2(0, 12));
        spriteBatch.Draw(px, ground, null, Color.Black * 0.22f, 0f, new Vector2(0.5f),
                         new Vector2(size, size * 0.35f), SpriteEffects.None, 0f);

        Texture2D tex = a.Stone == 2 ? _stoneLarge : _stoneSmall;
        float spin = a.Age * 7f;
        if (tex != null)
            spriteBatch.Draw(tex, p, null, Color.White, spin, new Vector2(tex.Width / 2f, tex.Height / 2f),
                             size / Math.Max(tex.Width, tex.Height), SpriteEffects.None, 0f);
        else
            spriteBatch.Draw(px, p, null, new Color(120, 118, 112), spin, new Vector2(0.5f), size, SpriteEffects.None, 0f);
    }

    // --- Geräte-Reparatur (F15): Dorfbewohner flicken beschädigte Belagerungsgeräte -------------

    /// <summary>Laufende Reparatur-Aufträge für Belagerungsgeräte: Gerät zu Auftrag
    /// (Kosten einmalig vorher abgezogen, wie bei der Gebäude-Reparatur). Gebäude
    /// haben ihren eigenen Auftrag (Building.Repair) — hier laufen nur die Geräte.</summary>
    private readonly Dictionary<Unit, AoE.Core.Economy.Repair> _deviceRepairs = new();

    /// <summary>
    /// Reparatur-Angebot für ein Gerät: Kosten = Ausbildungspreis × fehlender
    /// Anteil (je Rohstoff mindestens 1), Dauer = Ausbildungszeit × fehlender
    /// Anteil — genau die anteilige Regel der Gebäude-Reparatur, nur auf den
    /// Ausbildungs-Eintrag in <see cref="Products"/> gelegt. Für Typen ohne
    /// Ausbildungseintrag null.
    /// </summary>
    private static (Dictionary<Resource, int> Cost, float Seconds)? DeviceRepairQuote(Unit device)
    {
        foreach (var entry in Products)
        {
            if (entry.Unit != device.Type)
                continue;
            float fraction = BuildingRules.MissingFraction(device.Health, device.MaxHealth);
            if (fraction <= 0f)
                return null;
            var cost = new Dictionary<Resource, int>();
            foreach (var (res, amount) in entry.Cost)
                cost[res] = Math.Max(1, (int)Math.Ceiling(amount * fraction));
            return (cost, entry.Seconds * fraction);
        }
        return null;
    }

    /// <summary>Das eigene beschädigte Belagerungsgerät auf der angeklickten
    /// Kachel, oder null (F15).</summary>
    private Unit DamagedDeviceAt(Vector2 gridPos, int ownerId)
    {
        foreach (var u in units)
        {
            if (u.OwnerId != ownerId || !IsSiegeType(u.Type) || u.IsShip
                || u.State == UnitState.Dead || u.Health >= u.MaxHealth)
                continue;
            var cell = tileMap.WorldToGrid(u.Position);
            if ((int)cell.X == (int)gridPos.X && (int)cell.Y == (int)gridPos.Y)
                return u;
        }
        return null;
    }

    /// <summary>
    /// Reparatur-Auftrag für ein Gerät (F15), Spiegel von
    /// <see cref="RepairBuildingFor"/>: Kosten anteilig zum Schaden und einmalig
    /// vorher abgezogen; die Dauer läuft wie an der Gebäudefront mit
    /// SpeedFactor über die angekommenen Dorfbewohner. Ist der Auftrag schon
    /// da (weitere Arbeiter kommen dazu), kostet es nichts mehr. Rückgabe true,
    /// wenn der Auftrag angelegt oder beigetreten wurde.
    /// </summary>
    internal bool RepairDeviceFor(int ownerId, Unit device, List<Unit> workers)
    {
        if (device == null || device.OwnerId != ownerId || !IsSiegeType(device.Type))
            return false;
        if (device.Health >= device.MaxHealth)
            return false;   // kein Schaden — nichts zu reparieren und nichts zu zahlen

        if (!_deviceRepairs.ContainsKey(device))
        {
            var quote = DeviceRepairQuote(device);
            if (quote == null)
                return false;
            var owner = ownerId == 0 ? player1 : player2;
            if (!owner.Resources.PayCost(quote.Value.Cost))
            {
                if (ownerId == 0)
                {
                    var fehlt = new List<string>();
                    foreach (var (res, amt) in quote.Value.Cost)
                        if (owner.Resources[res] < amt)
                            fehlt.Add($"{amt} {ResourceName(res)}");
                    ShowHudMessage($"Zur Reparatur fehlen: {string.Join(" + ", fehlt)}");
                }
                return false;
            }
            _deviceRepairs[device] =
                new AoE.Core.Economy.Repair(quote.Value.Seconds, device.MaxHealth - device.Health);
            if (ownerId == 0)
                ShowHudMessage($"{device.Name} wird repariert ({CostText(quote.Value.Cost)})");
        }

        foreach (var u in workers ?? new List<Unit>())
            if (u.OwnerId == ownerId && u.Core is CoreVillager)
                SendToRepairDevice(u, device);
        return true;
    }

    /// <summary>Der Dorfbewohner bekommt den Weg zum Gerät; am Gerät stellt er
    /// sich im selben Abstand hin, in dem er auch an einer Wand steht. Ein
    /// laufender Sammelauftrag oder Baustelle endet (Befehl wie an der Baustelle).</summary>
    private void SendToRepairDevice(Unit worker, Unit device)
    {
        worker.Job = null;
        worker.BuildSite = null;
        worker.RepairTarget = device;
        var dir = worker.Position - device.Position;
        if (dir.Length() < 0.5f)
            dir = new Vector2(-1, 0);
        dir.Normalize();
        worker.TargetPosition = device.Position + dir * (0.9f * tileMap.TileSize);
        worker.Path = PathToStand(worker, worker.TargetPosition);
        worker.State = worker.Path.Count > 0 ? UnitState.Moving : UnitState.Building;
    }

    /// <summary>
    /// Geräte-Reparatur je Bild, Spiegel von <see cref="UpdateRepair"/>: gezählt
    /// werden nur Dorfbewohner, die am Gerät stehen (State Building). Die Stärke
    /// steigt mit dem Fortschritt über die Ausgangsstärke hinaus, nie über das
    /// Maximum. Ein totes oder weggefahrenes Gerät verliert den Auftrag, die
    /// Arbeiter werden untätig.
    /// </summary>
    private void UpdateDeviceRepair(float dt)
    {
        foreach (var (device, repair) in _deviceRepairs.ToList())
        {
            if (device.State == UnitState.Dead || !units.Contains(device))
            {
                _deviceRepairs.Remove(device);
                ReleaseDeviceRepairers(device);
                continue;
            }
            int repairers = units.Count(u => u.RepairTarget == device && u.State == UnitState.Building);
            if (repair.Update(dt * _productionSpeed, repairers))
            {
                device.Health = Math.Min(device.MaxHealth, repair.HpAt(repair.Progress, device.MaxHealth));
                _deviceRepairs.Remove(device);
                ReleaseDeviceRepairers(device);
            }
            else if (repairers > 0)
            {
                int targetHp = repair.HpAt(repair.Progress, device.MaxHealth);
                if (targetHp > device.Health)
                    device.Health = Math.Min(device.MaxHealth, targetHp);
            }
        }
    }

    /// <summary>Geräte-Reparatur beendet (fertig, Gerät tot oder gefahren): die
    /// Arbeiter lassen ab und werden untätig.</summary>
    private void ReleaseDeviceRepairers(Unit device)
    {
        foreach (var u in units.Where(u => u.RepairTarget == device).ToList())
        {
            u.RepairTarget = null;
            u.Path.Clear();
            u.State = UnitState.Idle;
        }
    }
}
