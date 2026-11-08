using System;
using System.Collections.Generic;
using AgeOfEvolutions.Core.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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

        if (moving)
        {
            // Der Tribok fährt abgebaut auf seinem Karren
            if (unit.Type == UnitType.Trebuchet && _trebuchetPacked.TryGetValue(unit.OwnerId, out var packed))
            {
                figure = packed;
                scale = TREBUCHET_PACKED_LENGTH * cameraZoom / packed.Width;
            }
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
}
