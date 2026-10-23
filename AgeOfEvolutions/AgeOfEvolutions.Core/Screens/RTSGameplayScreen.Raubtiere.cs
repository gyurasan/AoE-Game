using System;
using System.Collections.Generic;
using System.Linq;
using AgeOfEvolutions.Core.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Resource = AoE.Core.Entities.Resource;
using UnitState = AoE.Core.Entities.UnitState;
using CorePosition = AoE.Core.Entities.Position;
using CoreVillager = AoE.Core.Entities.Villager;
using GatherJob = AoE.Core.Economy.GatherJob;

namespace AgeOfEvolutions.Core.Screens;

// Raubtiere (RT, Nutzer 2026-10-23): Bären und Wölfe schlagen zu, wenn ihnen eine Einheit zu
// nahe kommt, verfolgen aber niemanden (TileMap.UpdatePredators). Hier antwortet der
// Spielbildschirm darauf: Ton, Angriffswarnung, Gegenwehr der Soldaten, Angriffsbefehl auf ein
// Raubtier per Klick, Jagd bis zum Erlegen - ein erlegter Bär wird danach gleich zerlegt, wenn
// Dorfbewohner ihn erlegt haben. Dazu die Bilder: Stand-, Lauf- und Angriffsbilder, das Fleisch
// des Bären, der Lebensbalken und der umfallende Wolf.
public partial class RTSGameplayScreen
{
    // --- RT2 Kampf --------------------------------------------------------------------

    /// <summary>
    /// Anhalt-Abstand im Nahkampf gegen ein Raubtier, in Kacheln von der Mitte seiner Kachel: ein
    /// Bär ist breiter als eine Figur. Fernkämpfer halten ihre Bogenvorlage (Stats.Range).
    /// </summary>
    private const float PREY_MELEE_STOP_TILES = 0.75f;

    /// <summary>Mitte der Kachel (x, y) in Weltpixeln - dort steht das Raubtier.</summary>
    private Vector2 PreyCenter(int x, int y) => tileMap.GridToWorld(new Vector2(x, y));

    /// <summary>Anhalt-Entfernung gegen ein Raubtier in Weltpixeln.</summary>
    private float PreyStopDistance(Unit unit)
        => (unit.AttackRange > 0 ? unit.AttackRange : PREY_MELEE_STOP_TILES) * tileMap.TileSize;

    /// <summary>
    /// Steht die Einheit nah genug am Raubtier, um zuzuschlagen bzw. zu schießen? Wie
    /// InAttackRange: Anhalt plus Abstand im Verband plus 0,25 px Gleitkomma-Rand.
    /// </summary>
    private bool InPreyRange(Unit unit, Vector2 center)
        => Vector2.Distance(unit.Position, center)
           <= PreyStopDistance(unit) + _combatSpacingTiles * tileMap.TileSize + 0.25f;

    /// <summary>
    /// Wo das Raubtier, das unit angreift, gerade lebt - oder null.
    /// VERTRAG: unit.PreyTarget null → null; tileMap.LocatePredator(unit.PreyTarget) null → null;
    /// tileMap.IsLivePredator(p.X, p.Y) false → null; sonst p.
    /// </summary>
    internal (int X, int Y)? LivePrey(Unit unit)
    {
        if (unit.PreyTarget is null)
            return null;
        if (tileMap.LocatePredator(unit.PreyTarget) is not { } p)
            return null;
        if (!tileMap.IsLivePredator(p.X, p.Y))
            return null;
        return p;
    }

    /// <summary>
    /// Der Punkt, an dem unit gegen das Raubtier antritt.
    /// VERTRAG: dir = unit.Position - center; ist dir.Length() kleiner 0.5f, dir = new Vector2(1, 0);
    /// dir.Normalize(); p = center + dir * PreyStopDistance(unit); Rückgabe p mit X auf 0 bis
    /// tileMap.Width * tileMap.TileSize und Y auf 0 bis tileMap.Height * tileMap.TileSize geklemmt.
    /// </summary>
    private Vector2 PreyStandPoint(Unit unit, Vector2 center)
    {
        // Richtung von der Einheit weg vom Tier; bei exakt gleicher Position eine Fallback-Richtung.
        Vector2 dir = unit.Position - center;
        if (dir.Length() < 0.5f)
            dir = new Vector2(1, 0);
        dir.Normalize();
        Vector2 p = center + dir * PreyStopDistance(unit);
        // Auf die Kartenfläche klemmen.
        p.X = Math.Clamp(p.X, 0f, tileMap.Width * tileMap.TileSize);
        p.Y = Math.Clamp(p.Y, 0f, tileMap.Height * tileMap.TileSize);
        return p;
    }

    /// <summary>
    /// Raubtiere takten und ihre Schläge beantworten. UpdateUnits ruft es jedes Bild direkt
    /// nach AutoRespond auf.
    /// VERTRAG: strikes = tileMap.UpdatePredators(dt, units). Für jeden Schlag s:
    /// EmitSound("treffer", s.Victim.Position); RegisterDamageTaken(s.Victim.OwnerId). Gegenwehr nur
    /// für Soldaten: ist s.Killed false, s.Victim.AttackPower größer 0, s.Victim.Category weder
    /// UnitCategory.Worker noch UnitCategory.Civilian und sind UnitTarget, AttackTarget und
    /// PreyTarget des Opfers alle null, dann AttackAnimal(new List&lt;Unit&gt; { s.Victim }, s.X, s.Y).
    /// Dorfbewohner wehren sich nicht von selbst - sie arbeiten weiter, der Spieler kann sie auf
    /// das Tier ansetzen.
    /// </summary>
    internal void UpdatePredators(float dt)
    {
        List<PredatorStrike> strikes = tileMap.UpdatePredators(dt, units);
        foreach (PredatorStrike s in strikes)
        {
            EmitSound("treffer", s.Victim.Position);
            RegisterDamageTaken(s.Victim.OwnerId);
            // Gegenwehr nur für Soldaten: Dorfbewohner wehren sich nicht von selbst.
            if (!s.Killed
                && s.Victim.AttackPower > 0
                && s.Victim.Category is not (UnitCategory.Worker or UnitCategory.Civilian)
                && s.Victim.UnitTarget is null
                && s.Victim.AttackTarget is null
                && s.Victim.PreyTarget is null)
            {
                AttackAnimal(new List<Unit> { s.Victim }, s.X, s.Y);
            }
        }
    }

    /// <summary>
    /// Das lebende Raubtier unter einem Klick auf die Kachel gridPos, das ownerId gerade sieht -
    /// oder null. Ein Raubtier steht im unteren Teil seiner Kachel und ragt nach oben und zur
    /// Seite hinaus, deshalb zählen auch die Kachel darunter und die Nachbarn.
    /// VERTRAG: gx = (int)gridPos.X, gy = (int)gridPos.Y; der Reihe nach (gx, gy), (gx, gy + 1),
    /// (gx - 1, gy), (gx + 1, gy), (gx - 1, gy + 1), (gx + 1, gy + 1): die erste Kachel (x, y) mit
    /// tileMap.IsLivePredator(x, y) und tileMap.IsTileVisible(x, y, ownerId) → (x, y). Sonst null.
    /// </summary>
    internal (int X, int Y)? PredatorAt(Vector2 gridPos, int ownerId)
    {
        int gx = (int)gridPos.X;
        int gy = (int)gridPos.Y;
        // Das Tier ragt nach oben und zur Seite hinaus: Kachel darunter und Nachbarn zählen mit.
        (int x, int y)[] candidates = new[]
        {
            (gx, gy), (gx, gy + 1), (gx - 1, gy), (gx + 1, gy), (gx - 1, gy + 1), (gx + 1, gy + 1)
        };
        foreach ((int x, int y) in candidates)
        {
            if (tileMap.IsLivePredator(x, y) && tileMap.IsTileVisible(x, y, ownerId))
                return (x, y);
        }
        return null;
    }

    /// <summary>
    /// Schickt Einheiten gegen das Raubtier auf (x, y) - per Klick oder als Gegenwehr.
    /// VERTRAG: ist tileMap.IsLivePredator(x, y) false, nichts tun. tier = tileMap.GetTile(x, y).Animal,
    /// center = PreyCenter(x, y). Für jede Einheit a aus attackers mit a.AttackPower größer 0:
    /// a.Job = null, a.BuildSite = null, a.AttackTarget = null, a.UnitTarget = null,
    /// a.PreyTarget = tier, a.AttackTimer = 0f; stand = PreyStandPoint(a, center);
    /// a.TargetPosition = stand; a.Path = tileMap.FindPath(a.Position, stand). Ist a.Path.Count
    /// größer 0: a.State = UnitState.Moving. Sonst, wenn InPreyRange(a, center): a.State =
    /// UnitState.Attacking. Sonst a.PreyTarget = null und a.State = UnitState.Idle.
    /// </summary>
    internal void AttackAnimal(List<Unit> attackers, int x, int y)
    {
        if (!tileMap.IsLivePredator(x, y))
            return;
        WildAnimal tier = tileMap.GetTile(x, y).Animal;
        Vector2 center = PreyCenter(x, y);
        foreach (Unit a in attackers)
        {
            if (a.AttackPower <= 0)
                continue;
            a.Job = null;
            a.BuildSite = null;
            a.AttackTarget = null;
            a.UnitTarget = null;
            a.PreyTarget = tier;
            a.AttackTimer = 0f;
            Vector2 stand = PreyStandPoint(a, center);
            a.TargetPosition = stand;
            a.Path = tileMap.FindPath(a.Position, stand);
            if (a.Path.Count > 0)
            {
                a.State = UnitState.Moving;
            }
            else if (InPreyRange(a, center))
            {
                a.State = UnitState.Attacking;
            }
            else
            {
                a.PreyTarget = null;
                a.State = UnitState.Idle;
            }
        }
    }

    /// <summary>
    /// Eine Einheit im Zustand Attacking kämpft gegen ihr Raubtier (PreyTarget): heranlaufen,
    /// Schlagtakt, Treffer. UpdateUnits ruft es, wenn sie weder ein Gebäude noch eine Einheit
    /// angreift.
    /// VERTRAG:
    /// 1. prey = LivePrey(unit); ist es null: PreyDone(unit), zurück.
    /// 2. center = PreyCenter(prey.X, prey.Y). Ist InPreyRange(unit, center) false: unit.Path =
    ///    tileMap.FindPath(unit.Position, PreyStandPoint(unit, center)); ist unit.Path.Count größer 0,
    ///    unit.State = UnitState.Moving, sonst unit.PreyTarget = null und unit.State =
    ///    UnitState.Idle; zurück.
    /// 3. before = unit.AttackTimer; unit.AttackTimer += dt; MaybeLaunchArrow(unit, before). Ist
    ///    unit.AttackTimer mindestens AoE.Core.Combat.BuildingCombat.RELOAD_SECONDS:
    ///    unit.AttackTimer -= RELOAD_SECONDS; ein Fernkämpfer (unit.AttackType == AttackType.Ranged)
    ///    EmitSound("bogen", unit.Position), sonst EmitSound("treffer", center); animal =
    ///    unit.PreyTarget; kind = tileMap.GetTile(prey.X, prey.Y).Food; liefert
    ///    tileMap.DamagePredator(prey.X, prey.Y, unit.AttackPower) true, dann
    ///    PredatorKilled(kind, prey.X, prey.Y, animal) und PreyDone(unit).
    /// </summary>
    private void HandleAnimalAttack(Unit unit, float dt)
    {
        // 1. Ist das Raubtier noch da?
        if (LivePrey(unit) is not { } prey)
        {
            PreyDone(unit);
            return;
        }
        Vector2 center = PreyCenter(prey.X, prey.Y);
        // 2. Noch nicht in Reichweite: hinlaufen.
        if (!InPreyRange(unit, center))
        {
            unit.Path = tileMap.FindPath(unit.Position, PreyStandPoint(unit, center));
            if (unit.Path.Count > 0)
                unit.State = UnitState.Moving;
            else
            {
                unit.PreyTarget = null;
                unit.State = UnitState.Idle;
            }
            return;
        }
        // 3. Schlagtakt und Treffer.
        float before = unit.AttackTimer;
        unit.AttackTimer += dt;
        MaybeLaunchArrow(unit, before);
        if (unit.AttackTimer >= AoE.Core.Combat.BuildingCombat.RELOAD_SECONDS)
        {
            unit.AttackTimer -= AoE.Core.Combat.BuildingCombat.RELOAD_SECONDS;
            if (unit.AttackType == AttackType.Ranged)
                EmitSound("bogen", unit.Position);
            else
                EmitSound("treffer", center);
            WildAnimal animal = unit.PreyTarget;
            FoodSource kind = tileMap.GetTile(prey.X, prey.Y).Food;
            if (tileMap.DamagePredator(prey.X, prey.Y, unit.AttackPower))
            {
                PredatorKilled(kind, prey.X, prey.Y, animal);
                PreyDone(unit);
            }
        }
    }

    /// <summary>
    /// Die Jagd ist vorbei - das Raubtier ist erlegt oder fort. Ein Dorfbewohner zerlegt einen
    /// erlegten Bären gleich, wie ein Jäger in AoE.
    /// VERTRAG: cell = tileMap.LocatePredator(unit.PreyTarget); dann unit.PreyTarget = null und
    /// unit.Path.Clear(). Ist unit.Core ein CoreVillager, cell nicht null und trägt
    /// tileMap.GetTile(cell.X, cell.Y) Nahrung (ResourceType Resource.Food und ResourceAmount
    /// größer 0): unit.Job = new GatherJob(unit.OwnerId, Resource.Food, new CorePosition(cell.X,
    /// cell.Y)); FollowJob(unit). Sonst unit.State = UnitState.Idle.
    /// </summary>
    private void PreyDone(Unit unit)
    {
        CoreVillager? villager = unit.Core as CoreVillager;
        (int X, int Y)? cell = villager is not null ? tileMap.LocatePredator(unit.PreyTarget) : null;
        unit.PreyTarget = null;
        unit.Path.Clear();
        // Ein Dorfbewohner zerlegt einen erlegten Bären gleich, wie ein Jäger in AoE.
        if (villager is not null
            && cell is { } c
            && tileMap.GetTile(c.X, c.Y).ResourceType == Resource.Food
            && tileMap.GetTile(c.X, c.Y).ResourceAmount > 0)
        {
            unit.Job = new GatherJob(unit.OwnerId, Resource.Food, new CorePosition(c.X, c.Y));
            FollowJob(unit);
            return;
        }
        unit.State = UnitState.Idle;
    }

    /// <summary>
    /// Ein Raubtier ist eben erlegt worden.
    /// VERTRAG: EmitSound("tod", PreyCenter(x, y)); ist kind FoodSource.Wolf, dazu
    /// AddFallen(x, y, animal) - der Wolf sinkt zu Boden, statt einfach zu verschwinden (RT3).
    /// </summary>
    private void PredatorKilled(FoodSource kind, int x, int y, WildAnimal animal)
    {
        EmitSound("tod", PreyCenter(x, y));
        // Der Wolf sinkt zu Boden, statt einfach zu verschwinden (RT3).
        if (kind == FoodSource.Wolf)
            AddFallen(x, y, animal);
    }

    // --- RT3 Bilder -------------------------------------------------------------------

    // Bären und Wölfe aus tools/bilder (Gruppen tiere, tiere_lauf, fleisch), nach rechts blickend.
    // Ihre Breite als Anteil an der Kachel wie bei den übrigen Tieren - ein Bär ist breiter als
    // eine Kachel, ein Wolf so breit wie ein Reh
    private static readonly string[] BearAssets = { "Tiere/baer", "Tiere/baer2" };
    private static readonly string[] WolfAssets = { "Tiere/wolf", "Tiere/wolf2" };
    private const float BEAR_WIDTH = 1.3f;
    private const float WOLF_WIDTH = 1.0f;
    private Texture2D[] _bearSprites = Array.Empty<Texture2D>();
    private Texture2D[] _wolfSprites = Array.Empty<Texture2D>();
    private Texture2D _bearMeat;
    // Angriffsbild je Standbild (Tiere/<name>_angriff), deckungsgleich mit ihm zugeschnitten
    private readonly Dictionary<Texture2D, Texture2D> _animalStrike = new();

    /// <summary>
    /// Lädt die Bilder der Raubtiere; LoadContent ruft es nach den Laufbildern der übrigen Tiere.
    /// Fehlt ein Bild, zeichnet das Spiel das Tier nicht bzw. ohne Lauf- und Angriffsbild.
    /// VERTRAG: _bearSprites = BearAssets.Select(LoadOptional).Where(t =&gt; t != null).ToArray(),
    /// _wolfSprites ebenso aus WolfAssets; _bearMeat = LoadOptional("Tiere/fleisch_baer"). Für jedes
    /// asset aus BearAssets und dann WolfAssets mit stand = LoadOptional(asset) nicht null: gibt es
    /// LoadOptional(asset + "_lauf1") und LoadOptional(asset + "_lauf2"), dann
    /// _animalWalk[stand] = new[] { lauf1, stand, lauf2, stand }; gibt es
    /// LoadOptional(asset + "_angriff"), dann _animalStrike[stand] = dieses Bild.
    /// </summary>
    private void LoadPredatorSprites()
    {
        _bearSprites = BearAssets.Select(LoadOptional).Where(t => t != null).ToArray();
        _wolfSprites = WolfAssets.Select(LoadOptional).Where(t => t != null).ToArray();
        _bearMeat = LoadOptional("Tiere/fleisch_baer");
        foreach (string asset in BearAssets.Concat(WolfAssets))
        {
            if (LoadOptional(asset) is not { } stand)
                continue;
            if (LoadOptional(asset + "_lauf1") is { } lauf1 && LoadOptional(asset + "_lauf2") is { } lauf2)
                _animalWalk[stand] = new[] { lauf1, stand, lauf2, stand };
            if (LoadOptional(asset + "_angriff") is { } ang)
                _animalStrike[stand] = ang;
        }
    }

    /// <summary>
    /// Ein erlegter Wolf, der zu Boden sinkt und verblasst: sein Bild, wo seine Pfoten standen
    /// (Weltpixel), seine Kachel, Blickrichtung, Breite in Weltpixeln und Alter in Sekunden.
    /// </summary>
    private struct FallenAnimal
    {
        public Texture2D Sprite;
        public Vector2 Foot;
        public int X, Y;
        public bool FacingLeft;
        public float Width;
        public float Age;
    }
    private readonly List<FallenAnimal> _fallen = new();
    /// <summary>Sekunden, in denen ein erlegter Wolf zu Boden sinkt, und danach, bis er verblasst ist.</summary>
    private const float FALL_SECONDS = 0.4f;
    private const float FADE_SECONDS = 1.2f;

    /// <summary>
    /// Ein Wolf ist eben auf (x, y) erlegt worden - er soll nicht einfach verschwinden.
    /// VERTRAG: nichts, wenn animal null oder _wolfSprites leer ist. Sonst _fallen.Add(new
    /// FallenAnimal { Sprite = _wolfSprites[animal.Look % _wolfSprites.Length], Foot = PreyCenter(x, y)
    /// + new Vector2(0, tileMap.TileSize / 4f), X = x, Y = y, FacingLeft = animal.FacingLeft,
    /// Width = WOLF_WIDTH * tileMap.TileSize, Age = 0f }).
    /// </summary>
    private void AddFallen(int x, int y, WildAnimal animal)
    {
        if (animal is null || _wolfSprites.Length == 0)
            return;
        _fallen.Add(new FallenAnimal
        {
            Sprite = _wolfSprites[animal.Look % _wolfSprites.Length],
            Foot = PreyCenter(x, y) + new Vector2(0, tileMap.TileSize / 4f),
            X = x,
            Y = y,
            FacingLeft = animal.FacingLeft,
            Width = WOLF_WIDTH * tileMap.TileSize,
            Age = 0f
        });
    }

    /// <summary>
    /// Die erlegten Wölfe altern; UpdateUnits ruft es direkt nach UpdatePredators.
    /// VERTRAG: für jeden Eintrag von _fallen Age += dt (es ist ein struct: den Eintrag per Index
    /// lesen, ändern und zurückschreiben); danach alle mit Age größer oder gleich
    /// FALL_SECONDS + FADE_SECONDS aus _fallen entfernen.
    /// </summary>
    internal void UpdateFallen(float dt)
    {
        for (int i = 0; i < _fallen.Count; i++)
        {
            var f = _fallen[i];
            f.Age += dt;
            _fallen[i] = f;
        }
        _fallen.RemoveAll(f => f.Age >= FALL_SECONDS + FADE_SECONDS);
    }

    /// <summary>
    /// Zeichnet die erlegten Wölfe, die Spieler 0 gerade sieht: sie sinken in FALL_SECONDS zu
    /// Boden (werden flacher, neigen sich und dunkeln nach) und verblassen dann in FADE_SECONDS.
    /// Draw ruft es direkt nach DrawTileMap - sie liegen am Boden, ohne Schlagschatten.
    /// VERTRAG: für jedes f aus _fallen mit tileMap.IsTileVisible(f.X, f.Y, 0):
    /// sink = Math.Min(1f, f.Age / FALL_SECONDS); alpha = 1f - Math.Clamp((f.Age - FALL_SECONDS) /
    /// FADE_SECONDS, 0f, 1f); foot = WorldToScreen(f.Foot); width = f.Width * cameraZoom; height =
    /// width * f.Sprite.Height / f.Sprite.Width * (1f - 0.55f * sink); shade = 1f - 0.35f * sink;
    /// spriteBatch.Draw(f.Sprite, new Rectangle((int)foot.X, (int)foot.Y, (int)width, (int)height),
    /// null, new Color(shade, shade, shade) * alpha, 0.35f * sink * (f.FacingLeft ? -1f : 1f),
    /// new Vector2(f.Sprite.Width / 2f, f.Sprite.Height), f.FacingLeft ?
    /// SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f) - der Ursprung unten in der Mitte
    /// des Bilds liegt auf foot.
    /// </summary>
    private void DrawFallen(SpriteBatch spriteBatch)
    {
        foreach (var f in _fallen)
        {
            if (!tileMap.IsTileVisible(f.X, f.Y, 0))
                continue;
            float sink = Math.Min(1f, f.Age / FALL_SECONDS);
            float alpha = 1f - Math.Clamp((f.Age - FALL_SECONDS) / FADE_SECONDS, 0f, 1f);
            Vector2 foot = WorldToScreen(f.Foot);
            float width = f.Width * cameraZoom;
            float height = width * f.Sprite.Height / f.Sprite.Width * (1f - 0.55f * sink);
            float shade = 1f - 0.35f * sink;
            spriteBatch.Draw(f.Sprite, new Rectangle((int)foot.X, (int)foot.Y, (int)width, (int)height),
                null, new Color(shade, shade, shade) * alpha, 0.35f * sink * (f.FacingLeft ? -1f : 1f),
                new Vector2(f.Sprite.Width / 2f, f.Sprite.Height),
                f.FacingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// Lebensbalken über einem verwundeten Raubtier, wie bei den Einheiten, aber rot - es ist
    /// keine eigene Figur. DrawAnimal ruft es am Ende mit dem Zielrechteck des Tierbilds.
    /// VERTRAG: nichts, wenn tile.Food.IsPredator() nicht gilt, tile.Animal null ist, Slaughtered
    /// gilt, Health kleiner oder gleich 0 ist oder Health mindestens tileMap.PredatorMaxHealth(tile.Food).
    /// Sonst barHeight = Math.Max(3, (int)(3 * cameraZoom)); barWidth = Math.Max(8, target.Width / 2);
    /// bar = new Rectangle(target.Center.X - barWidth / 2, target.Y - barHeight - 2, barWidth,
    /// barHeight); spriteBatch.Draw(px, bar, new Color(20, 20, 20)); dann der Anteil Health /
    /// (float)PredatorMaxHealth als Rechteck von bar.X mit Breite (int)(barWidth * Anteil) und Höhe
    /// barHeight in new Color(200, 50, 40).
    /// </summary>
    private void DrawPredatorHealth(SpriteBatch spriteBatch, Data.Tile tile, Rectangle target)
    {
        if (!tile.Food.IsPredator()
            || tile.Animal is null
            || tile.Animal.Slaughtered
            || tile.Animal.Health <= 0
            || tile.Animal.Health >= tileMap.PredatorMaxHealth(tile.Food))
            return;
        int barHeight = Math.Max(3, (int)(3 * cameraZoom));
        int barWidth = Math.Max(8, target.Width / 2);
        Rectangle bar = new(target.Center.X - barWidth / 2, target.Y - barHeight - 2, barWidth, barHeight);
        spriteBatch.Draw(px, bar, new Color(20, 20, 20));
        float anteil = tile.Animal.Health / (float)tileMap.PredatorMaxHealth(tile.Food);
        spriteBatch.Draw(px, new Rectangle(bar.X, bar.Y, (int)(barWidth * anteil), barHeight), new Color(200, 50, 40));
    }
}
