using System;
using System.Collections.Generic;
using AgeOfEvolutions.Core.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Age = AoE.Core.Economy.Age;

namespace AgeOfEvolutions.Core.Screens;

// Animationen (A1-A3, Nutzer 2026-10-17: „ich würde noch gerne mehr Animationen im Game haben"):
// Baustellen in vier Bauphasen, einstürzende Gebäude mit Staub und Trümmern, fliegende Pfeile.
public partial class RTSGameplayScreen
{
    // --- A1 Bauphasen ----------------------------------------------------------------

    // Wie viel des fertigen Gebäudebilds je Bauphase von unten zu sehen ist, und in welchem Ton:
    // 0 abgesteckter Platz (nichts), 1 Fundament, 2 Rohbau, 3 fast fertig
    private static readonly float[] StageReveal = { 0f, 0.3f, 0.65f, 1f };
    private static readonly Color[] StageTint =
    {
        Color.White, new Color(150, 128, 100), new Color(196, 180, 154), new Color(238, 233, 224),
    };

    /// <summary>
    /// Bauphase 0 bis 3 aus dem Baufortschritt, je ein Viertel.
    /// VERTRAG: progress &lt; 0.25f: 0; &lt; 0.5f: 1; &lt; 0.75f: 2; sonst 3.
    /// </summary>
    internal static int ConstructionStage(float progress) => progress < 0.25f ? 0 : progress < 0.5f ? 1 : progress < 0.75f ? 2 : 3;

    /// <summary>
    /// Das Bild, das ein fertiges Gebäude b zeigt (die Mühle ohne Flügel), oder null.
    /// VERTRAG: age = AgeOf(b.OwnerId); fehlt _buildingSprites[(b.Type, age, b.OwnerId)]: null.
    /// Ist b.Type "Mühle" und gibt es _millBare[(age, b.OwnerId)]: dieses Bild; sonst das aus
    /// _buildingSprites.
    /// </summary>
    private Texture2D BuildingSpriteOf(Data.Building b)
    {
        Age age = AgeOf(b.OwnerId);
        // Mühle: das Bild ohne Flügel, falls vorhanden
        if (b.Type == "Mühle" && _millBare.TryGetValue((age, b.OwnerId), out Texture2D mill))
            return mill;
        // sonst das normale Gebäudebild, fehlt es: null
        return _buildingSprites.TryGetValue((b.Type, age, b.OwnerId), out Texture2D sprite) ? sprite : null;
    }

    /// <summary>
    /// Eine Baustelle in ihrer Bauphase (A1): der abgesteckte Platz mit Material, dann das Gebäude
    /// von unten her im Gerüst. Ohne Gebäudebild der gezeichnete Bauplatz wie bisher.
    /// VERTRAG (alle Rechtecke mit spriteBatch.Draw; px ist das weiße 1x1-Bild):
    /// 1. sprite = BuildingSpriteOf(b); null: DrawConstructionSite(spriteBatch, rect, progress), fertig.
    /// 2. stage = ConstructionStage(progress); spriteHeight = rect.Width * sprite.Height / sprite.Width
    ///    (int); area = Rectangle(rect.X, rect.Bottom - spriteHeight, rect.Width, spriteHeight);
    ///    u = rect.Width / 128f; lokale Funktion int Px(float v) = Math.Max(1, (int)MathF.Round(v * u)).
    /// 3. Bauplatz: plot = Rectangle(rect.X + Px(6), rect.Y + Px(12), rect.Width - Px(12),
    ///    rect.Height - Px(16)). Zuerst PuffTexture() auf Rectangle(plot.X - Px(10), plot.Y - Px(8),
    ///    plot.Width + Px(20), plot.Height + Px(16)) in new Color(96, 76, 50) * 0.55f; dann px auf plot
    ///    in new Color(122, 100, 70) * 0.35f; dann 14 Erdkrümel k = 0..13: h = Hash(b.X, b.Y, 300 + k),
    ///    px auf Rectangle(plot.X + (int)(plot.Width * ((h % 900) / 1000f)),
    ///    plot.Y + (int)(plot.Height * (((h &gt;&gt; 10) % 900) / 1000f)), Px(3 + (h &gt;&gt; 20) % 3), Px(2))
    ///    in new Color(84, 66, 44) * 0.6f.
    /// 4. wood = new Color(122, 92, 56), woodDark = new Color(86, 64, 40).
    /// 5. stage == 0 (abgesteckt): inset = Px(10); vier Ecken c0 (plot.X + inset, plot.Y + inset),
    ///    c1 (plot.Right - inset, plot.Y + inset), c2 (plot.X + inset, plot.Bottom - inset),
    ///    c3 (plot.Right - inset, plot.Bottom - inset). Schnur in cord = new Color(232, 222, 190), je
    ///    Px(1) breit: c0 nach c1 und c2 nach c3 waagrecht (Rectangle(cA.X, cA.Y, cB.X - cA.X, Px(1))),
    ///    c0 nach c2 und c1 nach c3 senkrecht (Rectangle(cA.X, cA.Y, Px(1), cB.Y - cA.Y)). Je Ecke ein
    ///    Pflock Rectangle(c.X - Px(1.5f), c.Y - Px(9), Px(3), Px(10)) in woodDark. Stämme: sx =
    ///    plot.X + Px(16), sy = plot.Bottom - Px(20); i = 0..3: Rectangle(sx + Px(i * 3), sy - Px(i * 7),
    ///    Px(48 - i * 6), Px(7)) in (i gerade ? wood : woodDark), darauf die Schnittfläche
    ///    Rectangle(sx + Px(i * 3), sy - Px(i * 7), Px(4), Px(7)) in new Color(206, 170, 118). Steine:
    ///    stx = plot.Right - Px(52), sty = plot.Bottom - Px(20); für (dx, dy, s) in (0, 0, 12),
    ///    (13, 1, 11), (26, 0, 12), (6, -9, 12), (19, -9, 11), (12, -17, 10): Rectangle(stx + Px(dx),
    ///    sty + Px(dy), Px(s), Px(s * 0.8f)) in ((dx + dy) % 2 == 0 ? new Color(150, 148, 140)
    ///    : new Color(122, 120, 114)).
    /// 6. stage 1 bis 3: f = StageReveal[stage]; shown = Math.Max(1, (int)(spriteHeight * f)); source =
    ///    Rectangle(0, sprite.Height - (int)(sprite.Height * f), sprite.Width, (int)(sprite.Height * f));
    ///    sprite auf Rectangle(area.X, area.Bottom - shown, area.Width, shown) mit source und
    ///    StageTint[stage]. Gerüst: top = area.Bottom - shown - (stage == 3 ? 1 : Px(8)); bottom =
    ///    rect.Bottom - Px(4); pole = Px(2.5f); poles = stage == 3 ? { 0.06f, 0.94f } : { 0.06f, 0.5f,
    ///    0.94f }. Je p: x = area.X + (int)(area.Width * p) - pole / 2; Rectangle(x, top, pole, bottom - top)
    ///    in wood, darüber Rectangle(x + pole - Math.Max(1, pole / 3), top, Math.Max(1, pole / 3),
    ///    bottom - top) in woodDark. Nur stage &lt; 3: Schrägstreben, je i von 0 bis poles.Length - 2:
    ///    a = Vector2(area.X + area.Width * poles[i], bottom), e = Vector2(area.X + area.Width *
    ///    poles[i + 1], top + Px(6)), d = e - a; px an Position a, Quelle null, woodDark, Drehung
    ///    MathF.Atan2(d.Y, d.X), Ursprung Vector2(0f, 0.5f), Maßstab Vector2(d.Length(), Px(2)),
    ///    SpriteEffects.None, 0f. Bretter: plank = Px(3); levels = stage == 3 ? 1 : 1 + stage; je i =
    ///    0..levels - 1: y = top + Px(6) + (bottom - top - Px(10)) * i / Math.Max(1, levels) (int);
    ///    x0 = area.X + (int)(area.Width * (stage == 3 ? 0.02f : 0.03f)); w = stage == 3 ?
    ///    (int)(area.Width * 0.12f) : (int)(area.Width * 0.94f); Rectangle(x0, y, w, plank) in wood,
    ///    Rectangle(x0, y + plank - Math.Max(1, plank / 3), w, Math.Max(1, plank / 3)) in woodDark; bei
    ///    stage == 3 zusätzlich Rectangle(area.X + (int)(area.Width * 0.86f), y, w, plank) in wood.
    /// 7. Zuletzt der Fortschrittsbalken wie bisher: spriteDraw(spriteBatch, px, BuildingPart(rect, 14,
    ///    4, 100, 8), new Color(30, 30, 30)); done = (int)(100 * progress); bei done &gt; 0
    ///    spriteDraw(spriteBatch, px, BuildingPart(rect, 14, 4, done, 8), new Color(230, 200, 70)).
    /// </summary>
    private void DrawConstructionStages(SpriteBatch spriteBatch, Data.Building b, Rectangle rect, float progress)
    {
        // 1. Ohne Gebäudebild: der gezeichnete Bauplatz wie bisher
        Texture2D sprite = BuildingSpriteOf(b);
        if (sprite == null)
        {
            DrawConstructionSite(spriteBatch, rect, progress);
            return;
        }

        // 2. Bauphase, Bildhöhe im Bauplatz und lokale Pixelhilfe
        int stage = ConstructionStage(progress);
        int spriteHeight = rect.Width * sprite.Height / sprite.Width;
        Rectangle area = new(rect.X, rect.Bottom - spriteHeight, rect.Width, spriteHeight);
        float u = rect.Width / 128f;
        int Px(float v) => Math.Max(1, (int)MathF.Round(v * u));

        // 3. Bauplatz: weicher Rand, Erde, Erdkrümel
        Rectangle plot = new(rect.X + Px(6), rect.Y + Px(12), rect.Width - Px(12), rect.Height - Px(16));
        spriteBatch.Draw(PuffTexture(), new Rectangle(plot.X - Px(10), plot.Y - Px(8), plot.Width + Px(20), plot.Height + Px(16)), new Color(96, 76, 50) * 0.55f);
        spriteBatch.Draw(px, plot, new Color(122, 100, 70) * 0.35f);
        for (int k = 0; k < 14; k++)
        {
            int h = Hash(b.X, b.Y, 300 + k);
            spriteBatch.Draw(px, new Rectangle(plot.X + (int)(plot.Width * ((h % 900) / 1000f)),
                plot.Y + (int)(plot.Height * (((h >> 10) % 900) / 1000f)), Px(3 + (h >> 20) % 3), Px(2)), new Color(84, 66, 44) * 0.6f);
        }

        // 4. Holztöne für Pflock, Stämme und Gerüst
        Color wood = new(122, 92, 56);
        Color woodDark = new(86, 64, 40);

        if (stage == 0)
        {
            // 5. Abgesteckter Platz: Ecken, Schnur, Pflöcke, Stämme, Steine
            int inset = Px(10);
            Point c0 = new(plot.X + inset, plot.Y + inset);
            Point c1 = new(plot.Right - inset, plot.Y + inset);
            Point c2 = new(plot.X + inset, plot.Bottom - inset);
            Point c3 = new(plot.Right - inset, plot.Bottom - inset);
            Color cord = new(232, 222, 190);
            // Schnur je Px(1) breit: waagrecht und senkrecht
            spriteBatch.Draw(px, new Rectangle(c0.X, c0.Y, c1.X - c0.X, Px(1)), cord);
            spriteBatch.Draw(px, new Rectangle(c2.X, c2.Y, c3.X - c2.X, Px(1)), cord);
            spriteBatch.Draw(px, new Rectangle(c0.X, c0.Y, Px(1), c2.Y - c0.Y), cord);
            spriteBatch.Draw(px, new Rectangle(c1.X, c1.Y, Px(1), c3.Y - c1.Y), cord);
            // Pflöcke an den Ecken
            Point[] corners = { c0, c1, c2, c3 };
            foreach (Point c in corners)
                spriteBatch.Draw(px, new Rectangle(c.X - Px(1.5f), c.Y - Px(9), Px(3), Px(10)), woodDark);
            // Stämme mit Schnittfläche
            int sx = plot.X + Px(16);
            int sy = plot.Bottom - Px(20);
            for (int i = 0; i < 4; i++)
            {
                spriteBatch.Draw(px, new Rectangle(sx + Px(i * 3), sy - Px(i * 7), Px(48 - i * 6), Px(7)), i % 2 == 0 ? wood : woodDark);
                spriteBatch.Draw(px, new Rectangle(sx + Px(i * 3), sy - Px(i * 7), Px(4), Px(7)), new Color(206, 170, 118));
            }
            // Steine
            int stx = plot.Right - Px(52);
            int sty = plot.Bottom - Px(20);
            (int dx, int dy, int s)[] stones =
            {
                (0, 0, 12), (13, 1, 11), (26, 0, 12), (6, -9, 12), (19, -9, 11), (12, -17, 10),
            };
            foreach ((int dx, int dy, int s) in stones)
                spriteBatch.Draw(px, new Rectangle(stx + Px(dx), sty + Px(dy), Px(s), Px(s * 0.8f)),
                    (dx + dy) % 2 == 0 ? new Color(150, 148, 140) : new Color(122, 120, 114));
        }
        else
        {
            // 6. Fundament bis fast fertig: Bild von unten her im Gerüst
            float f = StageReveal[stage];
            int shown = Math.Max(1, (int)(spriteHeight * f));
            Rectangle source = new(0, sprite.Height - (int)(sprite.Height * f), sprite.Width, (int)(sprite.Height * f));
            // Schlagschatten (C7): der Teil des Gebäudes, der schon steht
            DrawCastShadow(spriteBatch, sprite, area, SpriteEffects.None, true, source.Y);
            spriteBatch.Draw(sprite, new Rectangle(area.X, area.Bottom - shown, area.Width, shown), source, StageTint[stage]);
            // Gerüst: Stangen, Schrägstreben, Bretter
            int top = area.Bottom - shown - (stage == 3 ? 1 : Px(8));
            int bottom = rect.Bottom - Px(4);
            int pole = Px(2.5f);
            float[] poles = stage == 3 ? new[] { 0.06f, 0.94f } : new[] { 0.06f, 0.5f, 0.94f };
            foreach (float p in poles)
            {
                int x = area.X + (int)(area.Width * p) - pole / 2;
                spriteBatch.Draw(px, new Rectangle(x, top, pole, bottom - top), wood);
                spriteBatch.Draw(px, new Rectangle(x + pole - Math.Max(1, pole / 3), top, Math.Max(1, pole / 3), bottom - top), woodDark);
            }
            if (stage < 3)
            {
                // Schrägstreben zwischen den Stangen
                for (int i = 0; i < poles.Length - 1; i++)
                {
                    Vector2 a = new(area.X + area.Width * poles[i], bottom);
                    Vector2 e = new(area.X + area.Width * poles[i + 1], top + Px(6));
                    Vector2 d = e - a;
                    spriteBatch.Draw(px, a, null, woodDark, MathF.Atan2(d.Y, d.X), new Vector2(0f, 0.5f), new Vector2(d.Length(), Px(2)), SpriteEffects.None, 0f);
                }
            }
            // Bretter
            int plank = Px(3);
            int levels = stage == 3 ? 1 : 1 + stage;
            for (int i = 0; i < levels; i++)
            {
                int y = top + Px(6) + (bottom - top - Px(10)) * i / Math.Max(1, levels);
                int x0 = area.X + (int)(area.Width * (stage == 3 ? 0.02f : 0.03f));
                int w = stage == 3 ? (int)(area.Width * 0.12f) : (int)(area.Width * 0.94f);
                spriteBatch.Draw(px, new Rectangle(x0, y, w, plank), wood);
                spriteBatch.Draw(px, new Rectangle(x0, y + plank - Math.Max(1, plank / 3), w, Math.Max(1, plank / 3)), woodDark);
                if (stage == 3)
                    spriteBatch.Draw(px, new Rectangle(area.X + (int)(area.Width * 0.86f), y, w, plank), wood);
            }
        }

        // 7. Fortschrittsbalken wie bisher
        spriteDraw(spriteBatch, px, BuildingPart(rect, 14, 4, 100, 8), new Color(30, 30, 30));
        int done = (int)(100 * progress);
        if (done > 0)
            spriteDraw(spriteBatch, px, BuildingPart(rect, 14, 4, done, 8), new Color(230, 200, 70));
    }

    // --- A2 Einsturz -------------------------------------------------------------------

    private sealed class Collapse
    {
        public Texture2D Sprite;   // das Bild des Gebäudes (oder null, dann nur Staub und Trümmer)
        public int X, Y, Width, Height;
        public float Age;          // Sekunden seit dem Einsturz
    }
    private readonly List<Collapse> _collapses = new();
    private Texture2D _puff;       // weiche runde Staubwolke

    internal const float COLLAPSE_SECONDS = 1.4f;   // so lange sinkt das Gebäude in sich zusammen
    internal const float DUST_SECONDS = 3.2f;       // so lange steht der Staub
    internal const float RUBBLE_SECONDS = 45f;      // so lange liegen die Trümmer
    internal const float RUBBLE_FADE_SECONDS = 8f;  // in den letzten Sekunden verblassen sie

    /// <summary>
    /// Merkt sich ein eben zerstörtes Gebäude für die Einsturz-Animation (A2).
    /// VERTRAG: _collapses bekommt new Collapse { Sprite = BuildingSpriteOf(b), X = b.X, Y = b.Y,
    /// Width = b.Width, Height = b.Height } (Age 0).
    /// </summary>
    private void StartCollapse(Data.Building b)
    {
        // das Bild des Gebäudes merken (oder null), damit es sichtbar einstürzt
        _collapses.Add(new Collapse
        {
            Sprite = BuildingSpriteOf(b),
            X = b.X,
            Y = b.Y,
            Width = b.Width,
            Height = b.Height,
        });
    }

    /// <summary>
    /// Weiche runde Wolke: Alpha von 1 in der Mitte nach 0 am Rand, einmal angelegt.
    /// VERTRAG: ist _puff nicht null, _puff. Sonst n = 48; je Bildpunkt (x, y): d = Abstand von
    /// (x + 0.5f, y + 0.5f) zu (n / 2f, n / 2f) geteilt durch n / 2f; a = Clamp(1 - d, 0, 1);
    /// a = a * a * (3 - 2 a); Farbe Color.White * a. _puff = new Texture2D(graphicsDevice, n, n) mit
    /// SetData dieser Farben (Zeile für Zeile, Index y * n + x); Rückgabe _puff.
    /// </summary>
    private Texture2D PuffTexture()
    {
        if (_puff != null)
            return _puff;
        int n = 48;
        Color[] data = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // Abstand zum Mittelpunkt, normiert auf den Radius
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = MathHelper.Clamp(1 - d, 0, 1);
                a = a * a * (3 - 2 * a);
                data[y * n + x] = Color.White * a;
            }
        _puff = new Texture2D(graphicsDevice, n, n);
        _puff.SetData(data);
        return _puff;
    }

    private static int Hash(int a, int b, int k) => unchecked(a * 73856093 ^ b * 19349663 ^ (k + 1) * 83492791) & 0x7FFFFFFF;

    /// <summary>
    /// Ein einstürzendes Gebäude (A2): Trümmer, darüber der sinkende Bau, darüber Staub.
    /// VERTRAG (spriteBatch.Draw; px weißes 1x1-Bild):
    /// 1. Nichts, wenn !tileMap.IsTileVisible(c.X + c.Width / 2, c.Y + c.Height / 2, 0).
    /// 2. rect = TileScreenRect(c.X, c.Y, c.Width, c.Height); u = rect.Width / 128f; Px(v) wie in
    ///    DrawConstructionStages; t = c.Age.
    /// 3. Trümmer, wenn t &gt;= COLLAPSE_SECONDS * 0.5f: alpha = Clamp((t - COLLAPSE_SECONDS * 0.5f) /
    ///    0.4f, 0, 1) * Clamp((RUBBLE_SECONDS - t) / RUBBLE_FADE_SECONDS, 0, 1).
    ///    a) Mauerreste, wenn c.Sprite nicht null: spriteHeight = rect.Width * c.Sprite.Height /
    ///       c.Sprite.Width; rest = 0.16f; source = Rectangle(0, (int)(c.Sprite.Height * (1 - rest)),
    ///       c.Sprite.Width, (int)(c.Sprite.Height * rest)); h = Math.Max(1, (int)(spriteHeight * rest));
    ///       c.Sprite auf Rectangle(rect.X, rect.Bottom - h, rect.Width, h) mit source in
    ///       new Color(92, 84, 76) * alpha.
    ///    b) Aschefleck: PuffTexture() auf Rectangle(rect.X - (int)(rect.Width * 0.1f), rect.Y +
    ///       (int)(rect.Height * 0.2f), (int)(rect.Width * 1.2f), (int)(rect.Height * 0.95f)) in
    ///       new Color(52, 42, 34) * (0.6f * alpha).
    ///    c) Steine: for (int k = 0; k &lt; 10 + 4 * c.Width; k++) - genau so viele: h = Hash(c.X, c.Y, k); r1 = (h % 1000) / 1000f;
    ///       r2 = ((h &gt;&gt; 10) % 1000) / 1000f; sx = 0.5f + (r1 - 0.5f) * 0.85f * (0.6f + 0.4f * r2);
    ///       sy = 0.5f + r2 * 0.42f; s = 5f + (h &gt;&gt; 20) % 7; Farbe nach (h &gt;&gt; 3) % 3: 0 new Color(70, 58,
    ///       46), 1 new Color(138, 132, 122), 2 new Color(112, 104, 94) (als switch: ((h &gt;&gt; 3) % 3) switch - mit Klammern); px auf Rectangle(rect.X +
    ///       (int)(rect.Width * sx), rect.Y + (int)(rect.Height * sy), Px(s), Px(s * 0.7f)) in Farbe * alpha.
    ///    d) Balken: for (int k = 0; k &lt; 2 + c.Width / 2; k++): h = Hash(c.X, c.Y, 100 + k); p = Vector2(rect.X +
    ///       rect.Width * (0.2f + (h % 600) / 1000f), rect.Y + rect.Height * (0.55f + ((h &gt;&gt; 10) % 300) /
    ///       1000f)); angle = ((h &gt;&gt; 4) % 100) / 100f * 1.6f - 0.8f; px an p, Quelle null,
    ///       new Color(48, 36, 28) * alpha, angle, Ursprung Vector2(0.5f), Maßstab Vector2(Px(42), Px(4)),
    ///       SpriteEffects.None, 0f.
    /// 4. Sinkender Bau, wenn t &lt; COLLAPSE_SECONDS und c.Sprite nicht null: s = t / COLLAPSE_SECONDS;
    ///    s = s * s; spriteHeight wie oben; f = 1 - s; shown = Math.Max(1, (int)(spriteHeight * f));
    ///    source = Rectangle(0, 0, c.Sprite.Width, Math.Max(1, (int)(c.Sprite.Height * f))) - der obere
    ///    Teil, der Bau versinkt; shake = (int)(MathF.Sin(t * 47f) * rect.Width * 0.025f * (1 - s));
    ///    Ton Color.Lerp(Color.White, new Color(110, 100, 90), s); c.Sprite auf Rectangle(rect.X + shake,
    ///    rect.Bottom - shown, rect.Width, shown) mit source.
    /// 5. Staub, wenn t &lt; DUST_SECONDS: puff = PuffTexture(); d = t / DUST_SECONDS; Wolken: for (int k = 0;
    ///    k &lt; 5 + 2 * c.Width; k++): h = Hash(c.X, c.Y, 200 + k); x = rect.X + rect.Width * (0.05f + (h % 900) /
    ///    1000f); rise = rect.Height * (0.2f + 0.9f * d) * (0.4f + ((h &gt;&gt; 10) % 600) / 1000f); y =
    ///    rect.Bottom - rect.Height * 0.15f - rise; r = rect.Width * (0.16f + 0.3f * d) * (0.7f +
    ///    ((h &gt;&gt; 20) % 500) / 1000f); a = 0.6f * MathF.Min(1f, t / 0.25f) * (1 - d) * (1 - d); puff auf
    ///    Rectangle((int)(x - r), (int)(y - r), (int)(2 * r), (int)(2 * r)) in new Color(176, 160, 136) * a.
    /// </summary>
    private void DrawCollapse(SpriteBatch spriteBatch, Collapse c)
    {
        // 1. Außerhalb des sichtbaren Bereichs: nichts zeichnen
        if (!tileMap.IsTileVisible(c.X + c.Width / 2, c.Y + c.Height / 2, 0))
            return;

        // 2. Bauplatz auf dem Bildschirm, lokale Pixelhilfe und Alter des Einsturzes
        Rectangle rect = TileScreenRect(c.X, c.Y, c.Width, c.Height);
        float u = rect.Width / 128f;
        int Px(float v) => Math.Max(1, (int)MathF.Round(v * u));
        float t = c.Age;

        // 3. Trümmer, wenn der Bau halbwegs versunken ist
        if (t >= COLLAPSE_SECONDS * 0.5f)
        {
            // einblenden, in den letzten Sekunden verblassen
            float alpha = MathHelper.Clamp((t - COLLAPSE_SECONDS * 0.5f) / 0.4f, 0, 1)
                * MathHelper.Clamp((RUBBLE_SECONDS - t) / RUBBLE_FADE_SECONDS, 0, 1);

            // a) Mauerreste: der untere Teil des Gebäudebilds, vergrault
            if (c.Sprite != null)
            {
                int spriteHeight = rect.Width * c.Sprite.Height / c.Sprite.Width;
                float rest = 0.16f;
                Rectangle source = new(0, (int)(c.Sprite.Height * (1 - rest)), c.Sprite.Width, (int)(c.Sprite.Height * rest));
                int h = Math.Max(1, (int)(spriteHeight * rest));
                spriteBatch.Draw(c.Sprite, new Rectangle(rect.X, rect.Bottom - h, rect.Width, h), source, new Color(92, 84, 76) * alpha);
            }

            // b) Aschefleck am Boden
            spriteBatch.Draw(PuffTexture(), new Rectangle(rect.X - (int)(rect.Width * 0.1f), rect.Y + (int)(rect.Height * 0.2f),
                (int)(rect.Width * 1.2f), (int)(rect.Height * 0.95f)), new Color(52, 42, 34) * (0.6f * alpha));

            // c) Steine, verteilt um die Mitte
            for (int k = 0; k < 10 + 4 * c.Width; k++)
            {
                int h = Hash(c.X, c.Y, k);
                float r1 = (h % 1000) / 1000f;
                float r2 = ((h >> 10) % 1000) / 1000f;
                float sx = 0.5f + (r1 - 0.5f) * 0.85f * (0.6f + 0.4f * r2);
                float sy = 0.5f + r2 * 0.42f;
                float s = 5f + (h >> 20) % 7;
                Color stone = ((h >> 3) % 3) switch
                {
                    0 => new Color(70, 58, 46),
                    1 => new Color(138, 132, 122),
                    _ => new Color(112, 104, 94),
                };
                spriteBatch.Draw(px, new Rectangle(rect.X + (int)(rect.Width * sx), rect.Y + (int)(rect.Height * sy), Px(s), Px(s * 0.7f)), stone * alpha);
            }

            // d) Balken, schräg liegend
            for (int k = 0; k < 2 + c.Width / 2; k++)
            {
                int h = Hash(c.X, c.Y, 100 + k);
                Vector2 p = new(rect.X + rect.Width * (0.2f + (h % 600) / 1000f), rect.Y + rect.Height * (0.55f + ((h >> 10) % 300) / 1000f));
                float angle = ((h >> 4) % 100) / 100f * 1.6f - 0.8f;
                spriteBatch.Draw(px, p, null, new Color(48, 36, 28) * alpha, angle, new Vector2(0.5f), new Vector2(Px(42), Px(4)), SpriteEffects.None, 0f);
            }
        }

        // 4. Sinkender Bau: wackelt und versinkt, der obere Teil verschwindet
        if (t < COLLAPSE_SECONDS && c.Sprite != null)
        {
            float s = t / COLLAPSE_SECONDS;
            s = s * s;
            int spriteHeight = rect.Width * c.Sprite.Height / c.Sprite.Width;
            float f = 1 - s;
            int shown = Math.Max(1, (int)(spriteHeight * f));
            Rectangle source = new(0, 0, c.Sprite.Width, Math.Max(1, (int)(c.Sprite.Height * f)));
            int shake = (int)(MathF.Sin(t * 47f) * rect.Width * 0.025f * (1 - s));
            Color tint = Color.Lerp(Color.White, new Color(110, 100, 90), s);
            spriteBatch.Draw(c.Sprite, new Rectangle(rect.X + shake, rect.Bottom - shown, rect.Width, shown), source, tint);
        }

        // 5. Staub, der aufquillt und verweht
        if (t < DUST_SECONDS)
        {
            Texture2D puff = PuffTexture();
            float d = t / DUST_SECONDS;
            for (int k = 0; k < 5 + 2 * c.Width; k++)
            {
                int h = Hash(c.X, c.Y, 200 + k);
                float x = rect.X + rect.Width * (0.05f + (h % 900) / 1000f);
                float rise = rect.Height * (0.2f + 0.9f * d) * (0.4f + ((h >> 10) % 600) / 1000f);
                float y = rect.Bottom - rect.Height * 0.15f - rise;
                float r = rect.Width * (0.16f + 0.3f * d) * (0.7f + ((h >> 20) % 500) / 1000f);
                float a = 0.6f * MathF.Min(1f, t / 0.25f) * (1 - d) * (1 - d);
                spriteBatch.Draw(puff, new Rectangle((int)(x - r), (int)(y - r), (int)(2 * r), (int)(2 * r)), new Color(176, 160, 136) * a);
            }
        }
    }

    // --- A3 Pfeile ---------------------------------------------------------------------

    private sealed class Arrow
    {
        public Vector2 From, To;   // Weltpixel: Bogen und Ziel
        public float Flight;       // Flugzeit in Sekunden
        public float Age;          // Sekunden seit dem Abschuss
        public bool Fletched;      // der Schütze hat „Befiederte Pfeile“ erforscht (A4)
    }
    private readonly List<Arrow> _arrows = new();

    // --- A4 Pfeilbilder (Nutzer 2026-10-20: „die Pfeile in high quality generieren. Die zwei
    // verschiedenen typen. Also nach der Weiterentwicklung müssen sie ja etwas anders
    // aussehen“): der einfache Pfeil und der nach der Forschung „Befiederte Pfeile“
    // (tools/bilder, Gruppe pfeile). Fehlt ein Bild, zeichnet DrawArrows die Striche aus A3.
    private Texture2D _arrowPlain, _arrowFletched;
    internal const float ARROW_LENGTH = 14f;            // Weltpixel, einfacher Pfeil
    internal const float ARROW_LENGTH_FLETCHED = 17f;   // der befiederte ist länger

    /// <summary>
    /// Länge eines Pfeils in Weltpixeln.
    /// VERTRAG: fletched ? ARROW_LENGTH_FLETCHED : ARROW_LENGTH.
    /// </summary>
    internal static float ArrowLength(bool fletched) => fletched ? ARROW_LENGTH_FLETCHED : ARROW_LENGTH;

    internal const float ARROW_SPEED = 260f;        // Weltpixel je Sekunde
    internal const float ARROW_MIN_FLIGHT = 0.15f;
    internal const float ARROW_MAX_FLIGHT = 0.6f;
    internal const float ARROW_ARC = 0.22f;         // Scheitelhöhe als Anteil der Flugstrecke

    /// <summary>
    /// Flugzeit eines Pfeils über distance Weltpixel.
    /// VERTRAG: MathHelper.Clamp(distance / ARROW_SPEED, ARROW_MIN_FLIGHT, ARROW_MAX_FLIGHT).
    /// </summary>
    internal static float ArrowFlight(float distance) => MathHelper.Clamp(distance / ARROW_SPEED, ARROW_MIN_FLIGHT, ARROW_MAX_FLIGHT);

    /// <summary>
    /// Wo der Pfeil a nach t Sekunden fliegt (Weltpixel), und in welche Richtung er zeigt: eine
    /// Wurfparabel von From nach To.
    /// VERTRAG: s = Clamp(t / a.Flight, 0, 1); h = ARROW_ARC * Abstand(a.From, a.To); Rückgabe
    /// Vector2.Lerp(a.From, a.To, s) + Vector2(0, -4 h s (1 - s)); angle = MathF.Atan2(d.Y, d.X) mit
    /// d = (a.To - a.From) + Vector2(0, -4 h (1 - 2 s)).
    /// </summary>
    private static Vector2 ArrowAt(Arrow a, float t, out float angle)
    {
        // Fortschritt auf der Strecke und Scheitelhöhe des Bogens
        float s = MathHelper.Clamp(t / a.Flight, 0, 1);
        float h = ARROW_ARC * Vector2.Distance(a.From, a.To);
        // Wurfparabel: waagerecht gleichmaessig, senkrecht der Bogen
        Vector2 pos = Vector2.Lerp(a.From, a.To, s) + new Vector2(0, -4 * h * s * (1 - s));
        // Richtung: Ableitung der Parabel
        Vector2 d = (a.To - a.From) + new Vector2(0, -4 * h * (1 - 2 * s));
        angle = MathF.Atan2(d.Y, d.X);
        return pos;
    }

    /// <summary>
    /// Wohin ein Schütze zielt: auf die Brust einer Figur oder in die Mitte eines Gebäudes.
    /// VERTRAG: unit.UnitTarget nicht null: dessen Position + Vector2(0, -10). Sonst unit.AttackTarget
    /// b nicht null: Vector2((b.X + b.Width / 2f) * tileMap.TileSize, (b.Y + b.Height / 2f) *
    /// tileMap.TileSize - tileMap.TileSize * 0.4f). Sonst LivePrey(unit) p nicht null (RT):
    /// PreyCenter(p.X, p.Y) + Vector2(0, -8). Sonst unit.Position.
    /// </summary>
    private Vector2 ArrowTarget(Unit unit)
    {
        // auf die Brust einer Figur
        if (unit.UnitTarget is { } u)
            return u.Position + new Vector2(0, -10);
        // in die Mitte eines Gebäudes
        if (unit.AttackTarget is { } b)
            return new Vector2((b.X + b.Width / 2f) * tileMap.TileSize, (b.Y + b.Height / 2f) * tileMap.TileSize - tileMap.TileSize * 0.4f);
        // auf ein Raubtier: in den Rumpf über der Mitte seiner Kachel (RT)
        if (LivePrey(unit) is { } prey)
            return PreyCenter(prey.X, prey.Y) + new Vector2(0, -8);
        // sonst die eigene Position
        return unit.Position;
    }

    /// <summary>
    /// Für eine Einheit, deren Schlagtakt eben von before auf unit.AttackTimer gestiegen ist (A3): ein
    /// Fernkämpfer lässt den Pfeil so los, dass er genau beim Treffer ankommt - wenn AttackTimer
    /// BuildingCombat.RELOAD_SECONDS erreicht. Der Treffer selbst bleibt, wo er ist.
    /// VERTRAG: nichts, wenn unit.AttackType != AttackType.Ranged. Sonst to = ArrowTarget(unit);
    /// from = unit.Position + Vector2(to.X &lt; unit.Position.X ? -5f : 5f, -13f); flight =
    /// ArrowFlight(Abstand(from, to)); launch = AoE.Core.Combat.BuildingCombat.RELOAD_SECONDS - flight.
    /// Gilt before &lt; launch und unit.AttackTimer &gt;= launch: _arrows bekommt new Arrow { From = from,
    /// To = to, Flight = MathF.Max(1f / 60f, RELOAD_SECONDS - unit.AttackTimer), Fletched =
    /// PlayerOf(unit).Techs.IsResearched(Tech.Fletching) } (Age 0) - die Forschung des Spielers,
    /// dem der Schütze gehört, nicht die des Gegners (A4).
    /// </summary>
    private void MaybeLaunchArrow(Unit unit, float before)
    {
        // nur Fernkämpfer schießen Pfeile
        if (unit.AttackType != AttackType.Ranged)
            return;
        // Bogen und Ziel, Flugzeit so, dass der Pfeil beim Treffer ankommt
        Vector2 to = ArrowTarget(unit);
        Vector2 from = unit.Position + new Vector2(to.X < unit.Position.X ? -5f : 5f, -13f);
        float flight = ArrowFlight(Vector2.Distance(from, to));
        float launch = AoE.Core.Combat.BuildingCombat.RELOAD_SECONDS - flight;
        // der Schlagtakt hat die Abschusszeit ueberquert: Pfeil loslassen
        if (before < launch && unit.AttackTimer >= launch)
            _arrows.Add(new Arrow
            {
                From = from,
                To = to,
                Flight = MathF.Max(1f / 60f, AoE.Core.Combat.BuildingCombat.RELOAD_SECONDS - unit.AttackTimer),
                // nach „Befiederte Pfeile“ fliegt das bessere Bild - je Spieler
                Fletched = PlayerOf(unit).Techs.IsResearched(AoE.Core.Economy.Tech.Fletching),
            });
    }

    /// <summary>
    /// Pfeile und Einstürze altern (A2, A3); was fertig ist, verschwindet.
    /// VERTRAG: jeder Pfeil Age += dt, dann alle mit Age &gt;= Flight aus _arrows entfernen; jeder
    /// Einsturz Age += dt, dann alle mit Age &gt;= RUBBLE_SECONDS aus _collapses entfernen.
    /// </summary>
    internal void UpdateEffects(float dt)
    {
        // Pfeile altern, abgeschossene verschwinden
        foreach (Arrow a in _arrows)
            a.Age += dt;
        _arrows.RemoveAll(a => a.Age >= a.Flight);

        // Einstürze altern, abgelaufene Trümmer verschwinden
        foreach (Collapse c in _collapses)
            c.Age += dt;
        _collapses.RemoveAll(c => c.Age >= RUBBLE_SECONDS);
    }

    /// <summary>
    /// Alle Pfeile im Flug, über den Figuren (A3): Schatten am Boden, Schaft, Spitze vorn, Federn hinten.
    /// VERTRAG: je Pfeil a in _arrows:
    /// 1. Überspringen, wenn weder die Kachel von a.From noch die von a.To (tileMap.WorldToGrid, als int)
    ///    für Spieler 0 sichtbar ist (tileMap.IsTileVisible(x, y, 0)).
    /// 2. p = WorldToScreen(ArrowAt(a, a.Age, out angle)); len = ArrowLength(a.Fletched) * cameraZoom;
    ///    thick = MathF.Max(1.5f, 1.2f * cameraZoom); dir = Vector2(MathF.Cos(angle), MathF.Sin(angle)).
    /// 3. Schatten: s = Clamp(a.Age / a.Flight, 0, 1); boden = WorldToScreen(Vector2.Lerp(a.From, a.To, s)
    ///    + Vector2(0, 12)); flach = a.To - a.From; px an boden, Quelle null, Color.Black * 0.22f,
    ///    MathF.Atan2(flach.Y, flach.X), Ursprung Vector2(0.5f), Maßstab Vector2(len, thick).
    /// 4. Bild (A4): bild = a.Fletched ? _arrowFletched : _arrowPlain. Ist es nicht null: bild an p,
    ///    Quelle null, Color.White, angle, Ursprung Vector2(bild.Width / 2f, bild.Height / 2f), Maßstab
    ///    len / bild.Width (eine Zahl, das Bild behält sein Seitenverhältnis) - und weiter mit dem
    ///    nächsten Pfeil. Die Bilder zeigen nach rechts (Winkel 0), die Spitze rechts, die Federn links.
    /// Ohne Bild die Striche aus A3:
    /// 5. Schaft: px an p - dir * len * 0.5f, new Color(200, 160, 104), angle, Ursprung Vector2(0, 0.5f),
    ///    Maßstab Vector2(len, thick).
    /// 6. Spitze: px an p + dir * len * 0.5f, new Color(214, 216, 222), angle, Ursprung Vector2(0.5f),
    ///    Maßstab Vector2(MathF.Max(2.5f, 3f * cameraZoom), MathF.Max(2f, 2.2f * cameraZoom)).
    /// 7. Federn: px an p - dir * len * 0.45f, a.Fletched ? new Color(150, 44, 34) : new Color(132, 120,
    ///    104), angle, Ursprung Vector2(0.5f), Maßstab Vector2(MathF.Max(2f, 3f * cameraZoom),
    ///    MathF.Max(1.5f, 2.2f * cameraZoom)).
    /// Alle mit SpriteEffects.None und Tiefe 0f.
    /// </summary>
    private void DrawArrows(SpriteBatch spriteBatch)
    {
        foreach (Arrow a in _arrows)
        {
            // 1. Außerhalb des sichtbaren Bereichs: überspringen
            Vector2 gf = tileMap.WorldToGrid(a.From);
            Vector2 gt = tileMap.WorldToGrid(a.To);
            if (!tileMap.IsTileVisible((int)gf.X, (int)gf.Y, 0) && !tileMap.IsTileVisible((int)gt.X, (int)gt.Y, 0))
                continue;

            // 2. Position und Richtung des Pfeils auf dem Bildschirm
            Vector2 p = WorldToScreen(ArrowAt(a, a.Age, out float angle));
            float len = ArrowLength(a.Fletched) * cameraZoom;
            float thick = MathF.Max(1.5f, 1.2f * cameraZoom);
            Vector2 dir = new(MathF.Cos(angle), MathF.Sin(angle));

            // 3. Schatten am Boden, flach entlang der Flugstrecke
            float s = MathHelper.Clamp(a.Age / a.Flight, 0, 1);
            Vector2 boden = WorldToScreen(Vector2.Lerp(a.From, a.To, s) + new Vector2(0, 12));
            Vector2 flach = a.To - a.From;
            spriteBatch.Draw(px, boden, null, Color.Black * 0.22f, MathF.Atan2(flach.Y, flach.X), new Vector2(0.5f), new Vector2(len, thick), SpriteEffects.None, 0f);

            // 4. Bild (A4): der einfache oder der befiederte Pfeil, die Bilder zeigen nach rechts
            Texture2D bild = a.Fletched ? _arrowFletched : _arrowPlain;
            if (bild != null)
            {
                spriteBatch.Draw(bild, p, null, Color.White, angle, new Vector2(bild.Width / 2f, bild.Height / 2f), len / bild.Width, SpriteEffects.None, 0f);
                continue;
            }

            // 5. Schaft
            spriteBatch.Draw(px, p - dir * len * 0.5f, null, new Color(200, 160, 104), angle, new Vector2(0, 0.5f), new Vector2(len, thick), SpriteEffects.None, 0f);

            // 6. Spitze vorn
            spriteBatch.Draw(px, p + dir * len * 0.5f, null, new Color(214, 216, 222), angle, new Vector2(0.5f), new Vector2(MathF.Max(2.5f, 3f * cameraZoom), MathF.Max(2f, 2.2f * cameraZoom)), SpriteEffects.None, 0f);

            // 7. Federn hinten
            spriteBatch.Draw(px, p - dir * len * 0.45f, null, a.Fletched ? new Color(150, 44, 34) : new Color(132, 120, 104), angle, new Vector2(0.5f), new Vector2(MathF.Max(2f, 3f * cameraZoom), MathF.Max(1.5f, 2.2f * cameraZoom)), SpriteEffects.None, 0f);
        }
    }

    // --- A5 Feuer und Rauch ------------------------------------------------------------

    // Nutzer (2026-10-20): „die Animation für die Feuer in der Schmiede fehlt. So wie der Kaminrauch
    // von den häusern“. Das gemalte Feuer der Schmiede flackert, glüht und sprüht Funken; aus den
    // Kaminen von Schmiede und Haus steigt Rauch, den der Wind nach rechts treibt. Alles hängt nur an
    // animationTime - kein Zustand, nichts im Spielstand. Baustellen brennen und rauchen nicht.

    // Die Kaminöffnung je Bild in Bildpunkten des Bilds, am Bild gemessen (blau und rot gleich). Jedes
    // Haus und jede Schmiede hat genau einen Kamin (Nutzer 2026-10-20: „achte darauf das jedes gebäude
    // nur einen Kamin hat“) - die Bilder ohne oder mit zwei Kaminen sind dafür neu gemalt (tools/bilder)
    private static readonly Dictionary<string, Vector2> ChimneyTops = new()
    {
        ["Gebaeude/dunkel/haus"] = new Vector2(176, 27),
        ["Gebaeude/feudal/haus"] = new Vector2(174, 30),
        ["Gebaeude/ritter/haus"] = new Vector2(163, 26),
        ["Gebaeude/imperial/haus"] = new Vector2(147, 57),
        ["Gebaeude/feudal/schmiede"] = new Vector2(244, 7),
        ["Gebaeude/ritter/schmiede"] = new Vector2(262, 39),
        ["Gebaeude/imperial/schmiede"] = new Vector2(258, 66),
    };
    // Feuerstelle der Schmiede je Bild in Bildpunkten: Fuß der gemalten Flammen (Mitte), ihre Breite und Höhe
    private static readonly Dictionary<string, (Vector2 Base, float Width, float Height)> ForgeFires = new()
    {
        ["Gebaeude/feudal/schmiede"] = (new Vector2(121, 256), 24f, 32f),
        ["Gebaeude/ritter/schmiede"] = (new Vector2(131, 292), 36f, 46f),
        ["Gebaeude/imperial/schmiede"] = (new Vector2(127, 266), 30f, 40f),
    };
    private readonly Dictionary<Texture2D, Vector2> _chimneys = new();
    private readonly Dictionary<Texture2D, (Vector2 Base, float Width, float Height)> _forgeFires = new();
    private Texture2D _flame;   // eine Flammenzunge, weiß: unten breit, oben spitz

    internal const int SMOKE_PUFFS = 10;         // so viele Wolken steigen je Kamin zugleich
    internal const float SMOKE_SECONDS = 5f;     // so lange steigt eine Wolke, bis sie verweht ist
    internal const float SMOKE_RISE = 220f;      // so hoch steigt sie, in Bildpunkten des Gebäudebilds
    internal const int FLAME_TONGUES = 5;        // Flammenzungen je Feuer
    internal const int SPARKS = 6;               // Funken je Feuer zugleich
    internal const float SPARK_SECONDS = 1.1f;   // so lange fliegt ein Funke

    /// <summary>
    /// Eine Rauchwolke (A5): wo sie gegenüber der Kaminöffnung steht, wie groß und wie deckend sie ist
    /// und wie alt (0 eben aus dem Kamin, gegen 1 verweht). time ist die Uhr des Kamins, k die Nummer
    /// der Wolke (0 bis SMOKE_PUFFS - 1), gust die Böe am Gebäude (Wind.Gust, 0 bis 1). Längen in
    /// Bildpunkten des Gebäudebilds, nach oben negativ.
    /// VERTRAG:
    /// 1. t = time + k * SMOKE_SECONDS / SMOKE_PUFFS; cycle = (int)MathF.Floor(t / SMOKE_SECONDS);
    ///    age = t / SMOKE_SECONDS - cycle.
    /// 2. h = Hash(cycle, k, 500); r = (h % 1000) / 1000f - jede Wolke ein wenig anders.
    /// 3. offset = new Vector2((24f + 80f * gust) * age * age + MathF.Sin(t * 1.3f + k * 2.1f) * 8f * age,
    ///    -SMOKE_RISE * age * (1f - 0.3f * gust)).
    /// 4. radius = (12f + 56f * age) * (0.85f + 0.3f * r).
    /// 5. alpha = 0.55f * MathHelper.Clamp(age / 0.1f, 0, 1) * (1 - age).
    /// Rückgabe (offset, radius, alpha, age).
    /// </summary>
    internal static (Vector2 Offset, float Radius, float Alpha, float Age) SmokePuff(float time, int k, float gust)
    {
        // 1. Uhr des Kamins, jede Wolke mit eigenem Versatz, Zyklus und Alter
        float t = time + k * SMOKE_SECONDS / SMOKE_PUFFS;
        int cycle = (int)MathF.Floor(t / SMOKE_SECONDS);
        float age = t / SMOKE_SECONDS - cycle;
        // 2. jede Wolke ein wenig anders
        int h = Hash(cycle, k, 500);
        float r = (h % 1000) / 1000f;
        // 3. aufsteigen, vom Wind nach rechts treiben, sanft wackeln
        Vector2 offset = new((24f + 80f * gust) * age * age + MathF.Sin(t * 1.3f + k * 2.1f) * 8f * age,
            -SMOKE_RISE * age * (1f - 0.3f * gust));
        // 4. wächst beim Steigen
        float radius = (12f + 56f * age) * (0.85f + 0.3f * r);
        // 5. einblenden, dann verwehen
        float alpha = 0.55f * MathHelper.Clamp(age / 0.1f, 0, 1) * (1 - age);
        return (offset, radius, alpha, age);
    }

    /// <summary>
    /// Wie hell das Schmiedefeuer zur Zeit time flackert, von 0 bis 1.
    /// VERTRAG: 0.5f + 0.22f * MathF.Sin(time * 11.3f) + 0.16f * MathF.Sin(time * 17.9f + 1.3f)
    /// + 0.12f * MathF.Sin(time * 29.1f + 0.7f), mit MathHelper.Clamp auf 0 bis 1 begrenzt.
    /// </summary>
    internal static float FireFlicker(float time)
    {
        // drei Sinuswellen im eigenen Takt, auf 0 bis 1 begrenzt
        float f = 0.5f + 0.22f * MathF.Sin(time * 11.3f) + 0.16f * MathF.Sin(time * 17.9f + 1.3f)
            + 0.12f * MathF.Sin(time * 29.1f + 0.7f);
        return MathHelper.Clamp(f, 0, 1);
    }

    /// <summary>
    /// Eine Flammenzunge (A5), j von 0 bis FLAME_TONGUES - 1: X der Versatz ihres Fußes von der Mitte
    /// der Feuerstelle und Height ihre Höhe, beide als Anteil an Breite und Höhe des Feuers, Tilt ihre
    /// Neigung in Bogenmaß (positiv nach rechts).
    /// VERTRAG:
    /// 1. s = j / (float)(FLAME_TONGUES - 1) - 0.5f (von -0,5 links bis 0,5 rechts).
    /// 2. x = s * 0.75f + MathF.Sin(time * 6.1f + j * 1.9f) * 0.05f.
    /// 3. flick = 0.5f + 0.5f * MathF.Sin(time * (8.3f + j * 2.7f) + j * 1.3f);
    ///    height = (1f - 0.9f * MathF.Abs(s)) * (0.6f + 0.4f * flick) - außen kürzer, jede im eigenen Takt.
    /// 4. tilt = s * 0.25f + MathF.Sin(time * 4.7f + j * 2.3f) * 0.12f - außen nach außen geneigt.
    /// Rückgabe (x, height, tilt).
    /// </summary>
    internal static (float X, float Height, float Tilt) FlameTongue(float time, int j)
    {
        // 1. von -0,5 links bis 0,5 rechts
        float s = j / (float)(FLAME_TONGUES - 1) - 0.5f;
        // 2. Fuß leicht hin und her
        float x = s * 0.75f + MathF.Sin(time * 6.1f + j * 1.9f) * 0.05f;
        // 3. außen kürzer, jede im eigenen Takt
        float flick = 0.5f + 0.5f * MathF.Sin(time * (8.3f + j * 2.7f) + j * 1.3f);
        float height = (1f - 0.9f * MathF.Abs(s)) * (0.6f + 0.4f * flick);
        // 4. außen nach außen geneigt
        float tilt = s * 0.25f + MathF.Sin(time * 4.7f + j * 2.3f) * 0.12f;
        return (x, height, tilt);
    }

    /// <summary>
    /// Ein Funke (A5), k von 0 bis SPARKS - 1: Versatz vom Fuß der Flammen als Anteil an Breite (X) und
    /// Höhe (Y, nach oben negativ) des Feuers, und wie hell er glüht (0 bis 1).
    /// VERTRAG:
    /// 1. t = time + k * 0.37f; cycle = (int)MathF.Floor(t / SPARK_SECONDS); f = t / SPARK_SECONDS - cycle.
    /// 2. h = Hash(cycle, k, 600); r1 = (h % 1000) / 1000f; r2 = ((h &gt;&gt; 10) % 1000) / 1000f.
    /// 3. offset = new Vector2((r1 - 0.5f) * 0.6f + MathF.Sin(f * 9f + k) * 0.08f, -f * (0.9f + 0.7f * r2)).
    /// 4. glow = (1 - f) * MathHelper.Clamp(f / 0.1f, 0, 1).
    /// Rückgabe (offset, glow).
    /// </summary>
    internal static (Vector2 Offset, float Glow) Spark(float time, int k)
    {
        // 1. jeder Funke im eigenen Takt
        float t = time + k * 0.37f;
        int cycle = (int)MathF.Floor(t / SPARK_SECONDS);
        float f = t / SPARK_SECONDS - cycle;
        // 2. jeder Funke ein wenig anders
        int h = Hash(cycle, k, 600);
        float r1 = (h % 1000) / 1000f;
        float r2 = ((h >> 10) % 1000) / 1000f;
        // 3. nach oben sprühen, leicht wackeln
        Vector2 offset = new((r1 - 0.5f) * 0.6f + MathF.Sin(f * 9f + k) * 0.08f, -f * (0.9f + 0.7f * r2));
        // 4. aufleuchten, dann erlöschen
        float glow = (1 - f) * MathHelper.Clamp(f / 0.1f, 0, 1);
        return (offset, glow);
    }

    /// <summary>
    /// Eine Flammenzunge als Bild, einmal angelegt: weiß, am breitesten bei drei Vierteln der Höhe, oben
    /// spitz, mit weichem Rand.
    /// VERTRAG: ist _flame nicht null, _flame. Sonst w = 16, hgt = 32; je Bildpunkt (x, y): v = (y + 0.5f) / hgt
    /// (0 oben, 1 unten); half = 0.48f * MathF.Sin(MathF.PI * MathF.Pow(v, 2.4f)); d = MathF.Abs((x + 0.5f) / w - 0.5f);
    /// a = half &lt;= 0.001f ? 0f : MathHelper.Clamp(1 - d / half, 0, 1); a = a * a * (3 - 2 * a); Farbe
    /// Color.White * a. _flame = new Texture2D(graphicsDevice, w, hgt) mit SetData dieser Farben (Index
    /// y * w + x); Rückgabe _flame.
    /// </summary>
    private Texture2D FlameTexture()
    {
        if (_flame != null)
            return _flame;
        int w = 16, hgt = 32;
        Color[] data = new Color[w * hgt];
        for (int y = 0; y < hgt; y++)
            for (int x = 0; x < w; x++)
            {
                // 0 oben, 1 unten; Breite der Zunge je Höhe, oben spitz
                float v = (y + 0.5f) / hgt;
                float half = 0.48f * MathF.Sin(MathF.PI * MathF.Pow(v, 2.4f));
                float d = MathF.Abs((x + 0.5f) / w - 0.5f);
                float a = half <= 0.001f ? 0f : MathHelper.Clamp(1 - d / half, 0, 1);
                a = a * a * (3 - 2 * a);
                data[y * w + x] = Color.White * a;
            }
        _flame = new Texture2D(graphicsDevice, w, hgt);
        _flame.SetData(data);
        return _flame;
    }

    /// <summary>
    /// Das Feuer der Schmiede (A5) über dem gemalten: Lichtschein, Flammenzungen und Funken. Alle Farben
    /// mit Alpha 0 - im vormultiplizierten AlphaBlend des Batches addieren sie sich, das Feuer leuchtet.
    /// VERTRAG (spriteBatch.Draw; px weißes 1x1-Bild):
    /// 1. Nichts, wenn _forgeFires keinen Eintrag für sprite hat (fire).
    /// 2. scale = area.Width / (float)sprite.Width; foot = new Vector2(area.X + fire.Base.X * scale,
    ///    area.Y + fire.Base.Y * scale); w = fire.Width * scale; hgt = fire.Height * scale;
    ///    t = animationTime + phase; flicker = FireFlicker(t).
    /// 3. Schein: PuffTexture() auf Rectangle((int)(foot.X - w * 1.3f), (int)(foot.Y - hgt * 1.25f),
    ///    (int)(w * 2.6f), (int)(hgt * 1.6f)) in new Color(255, 120, 40, 0) * (0.18f + 0.22f * flicker).
    /// 4. Zungen: flame = FlameTexture(); origin = new Vector2(flame.Width / 2f, flame.Height * 0.9f);
    ///    for (int j = 0; j &lt; FLAME_TONGUES; j++): (x, height, tilt) = FlameTongue(t, j);
    ///    p = new Vector2(foot.X + x * w, foot.Y); außen flame an p, Quelle null,
    ///    new Color(255, 110, 20, 0) * 0.55f, tilt, origin, Maßstab new Vector2(w * 0.34f / flame.Width,
    ///    hgt * height / flame.Height), SpriteEffects.None, 0f; dann innen dasselbe in
    ///    new Color(255, 214, 110, 0) * 0.5f mit Maßstab new Vector2(w * 0.17f / flame.Width,
    ///    hgt * height * 0.6f / flame.Height).
    /// 5. Funken: size = MathF.Max(1f, 4f * scale); for (int k = 0; k &lt; SPARKS; k++): (offset, glow) =
    ///    Spark(t, k); q = foot + new Vector2(offset.X * w, offset.Y * hgt); px auf
    ///    Rectangle((int)q.X, (int)q.Y, (int)size, (int)size) in new Color(255, 196, 90, 0) * glow.
    /// </summary>
    private void DrawForgeFire(SpriteBatch spriteBatch, Texture2D sprite, Rectangle area, float phase)
    {
        // 1. Nur Schmieden haben eine Feuerstelle
        if (!_forgeFires.TryGetValue(sprite, out var fire))
            return;
        // 2. Größe der Feuerstelle auf dem Bildschirm, Fuß der Flammen, Flackern
        float scale = area.Width / (float)sprite.Width;
        Vector2 foot = new(area.X + fire.Base.X * scale, area.Y + fire.Base.Y * scale);
        float w = fire.Width * scale;
        float hgt = fire.Height * scale;
        float t = animationTime + phase;
        float flicker = FireFlicker(t);
        // 3. Lichtschein, additiv
        spriteBatch.Draw(PuffTexture(), new Rectangle((int)(foot.X - w * 1.3f), (int)(foot.Y - hgt * 1.25f),
            (int)(w * 2.6f), (int)(hgt * 1.6f)), new Color(255, 120, 40, 0) * (0.18f + 0.22f * flicker));
        // 4. Flammenzungen: außen orange, innen gelb
        Texture2D flame = FlameTexture();
        Vector2 origin = new(flame.Width / 2f, flame.Height * 0.9f);
        for (int j = 0; j < FLAME_TONGUES; j++)
        {
            var (x, height, tilt) = FlameTongue(t, j);
            Vector2 p = new(foot.X + x * w, foot.Y);
            spriteBatch.Draw(flame, p, null, new Color(255, 110, 20, 0) * 0.55f, tilt, origin,
                new Vector2(w * 0.34f / flame.Width, hgt * height / flame.Height), SpriteEffects.None, 0f);
            spriteBatch.Draw(flame, p, null, new Color(255, 214, 110, 0) * 0.5f, tilt, origin,
                new Vector2(w * 0.17f / flame.Width, hgt * height * 0.6f / flame.Height), SpriteEffects.None, 0f);
        }
        // 5. Funken, die nach oben sprühen
        float size = MathF.Max(1f, 4f * scale);
        for (int k = 0; k < SPARKS; k++)
        {
            var (offset, glow) = Spark(t, k);
            Vector2 q = foot + new Vector2(offset.X * w, offset.Y * hgt);
            spriteBatch.Draw(px, new Rectangle((int)q.X, (int)q.Y, (int)size, (int)size), new Color(255, 196, 90, 0) * glow);
        }
    }

    /// <summary>
    /// Rauch aus dem Kamin eines Gebäudebilds (A5): SMOKE_PUFFS weiche Wolken, die aufsteigen, wachsen,
    /// vom Wind nach rechts treiben und verwehen - am Kamin dunkel, oben hell.
    /// VERTRAG:
    /// 1. Nichts, wenn _chimneys keinen Eintrag für sprite hat (chimney).
    /// 2. scale = area.Width / (float)sprite.Width; puff = PuffTexture(); gust = Wind.Gust(tile, animationTime);
    ///    top = new Vector2(area.X + chimney.X * scale, area.Y + chimney.Y * scale).
    /// 3. for (int k = 0; k &lt; SMOKE_PUFFS; k++): (offset, radius, alpha, age) = SmokePuff(animationTime + phase,
    ///    k, gust); weiter mit der nächsten Wolke, wenn alpha &lt;= 0.003f; c = top + offset * scale;
    ///    r = radius * scale; puff auf
    ///    Rectangle((int)(c.X - r), (int)(c.Y - r), (int)(2 * r), (int)(2 * r)) in
    ///    Color.Lerp(new Color(128, 124, 120), new Color(214, 212, 208), age) * alpha.
    /// </summary>
    private void DrawChimneySmoke(SpriteBatch spriteBatch, Texture2D sprite, Rectangle area, Vector2 tile, float phase)
    {
        // 1. Nur Gebäude mit Kamin rauchen
        if (!_chimneys.TryGetValue(sprite, out var chimney))
            return;
        // 2. Kaminöffnung auf dem Bildschirm, Böe am Gebäude
        float scale = area.Width / (float)sprite.Width;
        Texture2D puff = PuffTexture();
        float gust = Wind.Gust(tile, animationTime);
        Vector2 top = new(area.X + chimney.X * scale, area.Y + chimney.Y * scale);
        // 3. Wolken steigen, wachsen, treiben und verwehen
        for (int k = 0; k < SMOKE_PUFFS; k++)
        {
            var (offset, radius, alpha, age) = SmokePuff(animationTime + phase, k, gust);
            if (alpha <= 0.003f)
                continue;
            Vector2 c = top + offset * scale;
            float r = radius * scale;
            // am Kamin dunkel, oben hell
            spriteBatch.Draw(puff, new Rectangle((int)(c.X - r), (int)(c.Y - r), (int)(2 * r), (int)(2 * r)),
                Color.Lerp(new Color(128, 124, 120), new Color(214, 212, 208), age) * alpha);
        }
    }
}
