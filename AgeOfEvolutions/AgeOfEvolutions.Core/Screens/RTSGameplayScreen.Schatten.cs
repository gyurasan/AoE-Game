using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AgeOfEvolutions.Core.Screens;

// Schlagschatten (C7, Nutzer 2026-10-22: „die Schatten müssen schon alle in die gleiche
// Richtung zeigen“, die Ovale hatten „die falsche Form“, „sie dürfen keinen Abstand haben“,
// „prüf doch mal wie reale Schatten wirklich geworfen werden“; Vorbild errors/Schatten.jpg:
// Licht von links hinten, Schatten fallen nach rechts vorn). Wie Sonnenschatten:
// - Die Sonne ist so fern, dass alle Strahlen parallel fallen: jeder Schatten zeigt in
//   dieselbe Richtung, seine Länge wächst mit der Höhe. Das Licht kommt von links
//   hinten (die Qwen-Bilder sind links hell, rechts dunkel), die Schatten fallen nach
//   rechts vorn - Sonne etwa 67° hoch (Nutzer: „etwa halb so lang“), Schatten 25° unter
//   der Waagerechten: SHADOW_SKEW ≈ cos 25° / tan 67°, SHADOW_FLATTEN ≈ sin 25° / tan 67°.
// - Ein Schatten beginnt, wo das Objekt den Boden berührt: jeder Bildpunkt wird von
//   seinem Bodenkontakt aus auf den Boden gelegt, so hängt er ohne Lücke am Objekt, und
//   sein äußerer Rand ist der Umriss des Objekts. Weil er nach vorn fällt, liegt er vor
//   dem Objekt und ist ganz zu sehen.
// - Im Kernschatten fehlt nur das Sonnenlicht: überall gleich dunkel, und wo sich zwei
//   Schatten decken, wird es nicht dunkler. Alle Masken kommen deshalb mit Maximum in
//   ein eigenes Bild, das einmal über den Boden gelegt wird.
// - Die Sonnenscheibe ist klein, der Rand nur leicht weich (SHADOW_SOFT).
// - Was im Schatten liegt, beleuchtet noch der blaue Himmel: der Schatten ist kühl
//   und etwa halb so hell (SHADOW_TINT). Laub lässt Licht durch (TREE_SHADOW_DENSITY).
public partial class RTSGameplayScreen
{
    /// <summary>Wie weit der Schatten je Bildpunkt Höhe nach rechts fällt.</summary>
    internal const float SHADOW_SKEW = 0.38f;
    /// <summary>Wie weit der Schatten je Bildpunkt Höhe nach vorn (unten) fällt.</summary>
    internal const float SHADOW_FLATTEN = 0.175f;
    /// <summary>Tiefe eines Körpers (Krone, Figur, Tier) im Verhältnis zu seiner Breite,
    /// so wie der Boden ihn zeigt.</summary>
    internal const float SHADOW_DEPTH = 0.5f;
    /// <summary>Radius des weichen Rands in Maskenpunkten.</summary>
    internal const int SHADOW_SOFT = 1;
    /// <summary>Deckkraft des Schattens von Bäumen und Büschen: Laub lässt Licht durch.</summary>
    internal const float TREE_SHADOW_DENSITY = 0.85f;
    /// <summary>Farbe, mit der das Schattenbild über den Boden gelegt wird (vormultipliziert):
    /// A = wie viel Licht fehlt, RGB = das kühle Himmelslicht, das bleibt.</summary>
    internal static readonly Color SHADOW_TINT = new Color(10, 16, 34, 128);

    /// <summary>
    /// Die Deckkraft eines Bilds in halber Auflösung - der Schatten braucht keine feinere,
    /// und so belegt sein Bild ein Viertel des Speichers.
    /// VERTRAG: w2 = (width + 1) / 2, h2 = (height + 1) / 2; Ergebnis w2 × h2, zeilenweise.
    /// Punkt (x, y) = größtes A der bis zu vier Bildpunkte (2x + i, 2y + j) mit i, j ∈ {0, 1}
    /// innerhalb des Bilds; bei <paramref name="flip"/> wird die Spalte gespiegelt gelesen
    /// (pixels[py * width + (width - 1 - px)]), und Zeilen py &lt; <paramref name="hiddenTop"/>
    /// zählen nicht (Bauphasen: dieser Teil steht noch nicht).
    /// Reine Rechnung ohne Grafik - tools/spielablauf (Gruppe schatten) prüft sie.
    /// </summary>
    internal static byte[] HalfAlpha(Color[] pixels, int width, int height, bool flip, int hiddenTop)
    {
        // Halbe Auflösung: w2 × h2, zeilenweise; jeder Punkt nimmt das größte A der
        // bis zu vier darunterliegenden Bildpunkte.
        int w2 = (width + 1) / 2;
        int h2 = (height + 1) / 2;
        byte[] result = new byte[w2 * h2];
        for (int py = 0; py < h2; py++)
        {
            int y = py * 2;
            if (y >= height)
                continue;
            for (int px = 0; px < w2; px++)
            {
                int x = px * 2;
                byte best = 0;
                for (int j = 0; j < 2; j++)
                {
                    int yy = y + j;
                    if (yy >= height)
                        break;
                    // Verdeckte Zeilen (Bauphasen) zählen nicht - je Zeile einzeln.
                    if (yy < hiddenTop)
                        continue;
                    for (int i = 0; i < 2; i++)
                    {
                        int xx = x + i;
                        if (xx >= width)
                            break;
                        int index = flip ? yy * width + (width - 1 - xx) : yy * width + xx;
                        if (pixels[index].A > best)
                            best = pixels[index].A;
                    }
                }
                result[py * w2 + px] = best;
            }
        }
        return result;
    }

    /// <summary>
    /// Die Schattenmaske eines Bilds aus seiner Deckkraft <paramref name="alpha"/>
    /// (width × height, zeilenweise). Die Maske ist shadowWidth × shadowHeight groß und liegt
    /// mit ihrer linken oberen Ecke auf der des Bilds; nach rechts und unten ist Platz für
    /// den Schatten. 255 = voller Kernschatten.
    /// VERTRAG: shadowWidth = width + (int)MathF.Ceiling(height * SHADOW_SKEW), shadowHeight =
    /// height + (int)MathF.Ceiling(height * SHADOW_FLATTEN + width * SHADOW_DEPTH / 2f) + 2 * SHADOW_SOFT -
    /// unten ist so auch Platz für die halbe Tiefe der breitesten Krone und den weichen Rand, sonst
    /// würde ein breiter Kronenschatten gerade abgeschnitten (Nutzer 2026-10-22). Ein Punkt deckt,
    /// wenn alpha ≥ 128.
    /// Eine Zwischenmaske (shadowWidth × shadowHeight) bekommt 255 an jedem Schattenpunkt:
    /// - mit <paramref name="contour"/> (Gebäude, Haufen): Bodenkontakt ground[x] = unterste
    ///   deckende Zeile der Spalte - so steht jede Wand auf ihrer eigenen Unterkante. Danach
    ///   wird die Bodenlinie geglättet: foot = erste Spalte mit ground[x] == lowest (lowest =
    ///   größtes ground[x]); nach rechts für x = foot + 1 bis width - 1 und nach links für
    ///   x = foot - 1 bis 0, Spalten mit ground[x] &lt; 0 übersprungen: ground[x] = Math.Max(ground[x],
    ///   ground[prev] - |x - prev|), prev = die zuletzt behandelte Spalte mit Bodenkontakt (am
    ///   Anfang foot). Die Linie steigt so je Spalte um höchstens einen Punkt; ein Dachüberstand,
    ///   ein Fahnentuch oder ein Erker am Rand steht auf der Wand darunter statt auf dem Boden.
    ///   Ein Punkt
    ///   (x, y) zählt, wenn er deckt und y ≤ ground[x]; er fällt nach Fall(x, y) = (x +
    ///   (int)(h * SHADOW_SKEW), ground[x] + (int)(h * SHADOW_FLATTEN)) mit h = ground[x] - y
    ///   und setzt diesen Punkt. Zählt auch sein rechter Nachbar (x + 1, y), wird die Strecke
    ///   dazwischen gefüllt - steigt die Unterkante schräg an, lägen die Punkte sonst mehr als
    ///   einen auseinander, und der Schatten wäre schraffiert: (ax, ay) = Fall(x, y),
    ///   (bx, by) = Fall(x + 1, y), steps = Math.Max(|bx - ax|, |by - ay|), für k = 1 bis
    ///   steps - 1 (k &lt; steps) der Punkt (ax + (bx - ax) * k / steps, ay + (by - ay) * k / steps),
    ///   ganzzahlig geteilt.
    /// - ohne (Bäume, Figuren, Tiere, Büsche - Körper statt Scheiben): lowest = unterste
    ///   deckende Zeile des ganzen Bilds (Stammfuß, Füße, Hufe). Jede Zeile y ≤ lowest mit
    ///   deckenden Punkten, left/right = erste/letzte deckende Spalte, h = lowest - y, ist eine
    ///   Ellipse mit Mitte cx = (left + right + 1) / 2f + h * SHADOW_SKEW, cy = lowest + 0.5f +
    ///   h * SHADOW_FLATTEN, Halbachsen rx = (right - left + 1) / 2f, ry = Math.Max(0.5f,
    ///   rx * SHADOW_DEPTH). Zu ihr gehört jeder Maskenpunkt (px, py), 0 ≤ py &lt; shadowHeight, mit
    ///   dy = (py + 0.5f - cy) / ry, dy² ≤ 1, half = rx * MathF.Sqrt(1 - dy²) und
    ///   Math.Max(0, (int)MathF.Ceiling(cx - half - 0.5f)) ≤ px ≤
    ///   Math.Min(shadowWidth - 1, (int)MathF.Floor(cx + half - 0.5f)).
    /// Danach wird
    /// die Zwischenmaske weichgezeichnet, n = 2 * SHADOW_SOFT + 1: erst waagerecht
    /// zeile(x, y) = (Summe der Zwischenmaske über x - SHADOW_SOFT bis x + SHADOW_SOFT) / n,
    /// dann senkrecht wert(x, y) = (Summe von zeile über y - SHADOW_SOFT bis y + SHADOW_SOFT) / n,
    /// beides ganzzahlig geteilt, Punkte außerhalb der Maske zählen 0; Ergebnis (byte)wert.
    /// Ohne deckenden Punkt bleibt alles 0.
    /// Reine Rechnung ohne Grafik - tools/spielablauf (Gruppe schatten) prüft sie.
    /// </summary>
    internal static byte[] BakeShadowMask(byte[] alpha, int width, int height, bool contour, out int shadowWidth, out int shadowHeight)
    {
        // Die Maske ist so groß wie das Bild plus der Fall nach rechts und nach vorn.
        shadowWidth = width + (int)MathF.Ceiling(height * SHADOW_SKEW);
        shadowHeight = height + (int)MathF.Ceiling(height * SHADOW_FLATTEN + width * SHADOW_DEPTH / 2f) + 2 * SHADOW_SOFT;
        // Lokale Kopien: out-Parameter darf eine lokale Funktion nicht benutzen (CS1628)
        int sw = shadowWidth, sh = shadowHeight;

        // Deckt ein Bildpunkt?
        bool Covers(int x, int y) => alpha[y * width + x] >= 128;

        // Zwischenmaske: 255 an jedem Schattenpunkt.
        byte[] mask = new byte[sw * sh];
        void Set(int x, int y)
        {
            if (x >= 0 && x < sw && y >= 0 && y < sh)
                mask[y * sw + x] = 255;
        }

        if (contour)
        {
            // Bodenkontakt je Spalte: unterste deckende Zeile - jede Wand steht auf
            // ihrer eigenen Unterkante.
            int[] ground = new int[width];
            for (int x = 0; x < width; x++)
            {
                ground[x] = -1;
                for (int y = height - 1; y >= 0; y--)
                {
                    if (Covers(x, y))
                    {
                        ground[x] = y;
                        break;
                    }
                }
            }

            // Bodenlinie glätten: ein Dachüberstand, ein Fahnentuch oder ein Erker am
            // Rand steht auf der Wand darunter, nicht auf dem Boden - die Linie steigt
            // vom tiefsten Punkt nach außen je Spalte um höchstens einen Punkt.
            int lowest = -1;
            for (int x = 0; x < width; x++)
            {
                if (ground[x] > lowest)
                    lowest = ground[x];
            }
            if (lowest >= 0)
            {
                int foot = 0;
                while (foot < width && ground[foot] != lowest)
                    foot++;
                int prev = foot;
                for (int x = foot + 1; x < width; x++)
                {
                    if (ground[x] < 0)
                        continue;
                    ground[x] = Math.Max(ground[x], ground[prev] - (x - prev));
                    prev = x;
                }
                prev = foot;
                for (int x = foot - 1; x >= 0; x--)
                {
                    if (ground[x] < 0)
                        continue;
                    ground[x] = Math.Max(ground[x], ground[prev] - (prev - x));
                    prev = x;
                }
            }

            // Jeder Punkt fällt von seinem Bodenkontakt aus nach rechts vorn; zählt
            // auch der rechte Nachbar, wird die Strecke dazwischen gefüllt, damit der
            // Schatten geschlossen bleibt.
            for (int x = 0; x < width; x++)
            {
                int g = ground[x];
                if (g < 0)
                    continue;
                for (int y = 0; y <= g; y++)
                {
                    if (!Covers(x, y))
                        continue;
                    int h = g - y;
                    int ax = x + (int)(h * SHADOW_SKEW);
                    int ay = g + (int)(h * SHADOW_FLATTEN);
                    Set(ax, ay);
                    if (x + 1 < width && ground[x + 1] >= y && Covers(x + 1, y))
                    {
                        int h2 = ground[x + 1] - y;
                        int bx = x + 1 + (int)(h2 * SHADOW_SKEW);
                        int by = ground[x + 1] + (int)(h2 * SHADOW_FLATTEN);
                        int steps = Math.Max(Math.Abs(bx - ax), Math.Abs(by - ay));
                        for (int k = 1; k < steps; k++)
                            Set(ax + (bx - ax) * k / steps, ay + (by - ay) * k / steps);
                    }
                }
            }
        }
        else
        {
            // Körper statt Scheiben: lowest = unterste deckende Zeile des ganzen Bilds
            // (Stammfuß, Füße, Hufe) - von unten aufwärts suchen, beim ersten Treffer
            // abbrechen.
            int lowest = -1;
            for (int y = height - 1; y >= 0; y--)
            {
                bool any = false;
                for (int x = 0; x < width; x++)
                {
                    if (Covers(x, y))
                    {
                        any = true;
                        break;
                    }
                }
                if (any)
                {
                    lowest = y;
                    break;
                }
            }

            // Jede Zeile über lowest mit deckenden Punkten ist eine Ellipse, die von
            // lowest aus nach rechts vorn fällt.
            for (int y = 0; y <= lowest; y++)
            {
                int left = -1, right = -1;
                for (int x = 0; x < width; x++)
                {
                    if (Covers(x, y))
                    {
                        if (left < 0)
                            left = x;
                        right = x;
                    }
                }
                if (left < 0)
                    continue;
                int h = lowest - y;
                float cx = (left + right + 1) / 2f + h * SHADOW_SKEW;
                float cy = lowest + 0.5f + h * SHADOW_FLATTEN;
                float rx = (right - left + 1) / 2f;
                float ry = Math.Max(0.5f, rx * SHADOW_DEPTH);
                for (int py = 0; py < shadowHeight; py++)
                {
                    float dy = (py + 0.5f - cy) / ry;
                    if (dy * dy > 1f)
                        continue;
                    float half = rx * MathF.Sqrt(1f - dy * dy);
                    int x0 = Math.Max(0, (int)MathF.Ceiling(cx - half - 0.5f));
                    int x1 = Math.Min(shadowWidth - 1, (int)MathF.Floor(cx + half - 0.5f));
                    for (int px = x0; px <= x1; px++)
                        Set(px, py);
                }
            }
        }

        // Weichzeichnen, n = 2 * SHADOW_SOFT + 1: erst waagerecht, dann senkrecht;
        // Punkte außerhalb der Maske zählen 0, ganzzahlig geteilt.
        int n = 2 * SHADOW_SOFT + 1;
        byte[] row = new byte[shadowWidth * shadowHeight];
        for (int y = 0; y < shadowHeight; y++)
        {
            for (int x = 0; x < shadowWidth; x++)
            {
                int sum = 0;
                for (int i = -SHADOW_SOFT; i <= SHADOW_SOFT; i++)
                {
                    int xx = x + i;
                    if (xx < 0 || xx >= shadowWidth)
                        continue;
                    sum += mask[y * shadowWidth + xx];
                }
                row[y * shadowWidth + x] = (byte)(sum / n);
            }
        }
        for (int y = 0; y < shadowHeight; y++)
        {
            for (int x = 0; x < shadowWidth; x++)
            {
                int sum = 0;
                for (int j = -SHADOW_SOFT; j <= SHADOW_SOFT; j++)
                {
                    int yy = y + j;
                    if (yy < 0 || yy >= shadowHeight)
                        continue;
                    sum += row[yy * shadowWidth + x];
                }
                mask[y * shadowWidth + x] = (byte)(sum / n);
            }
        }
        return mask;
    }

    /// <summary>
    /// Wohin die Schattenmaske gezeichnet wird: linke obere Ecke wie das Bild selbst, im
    /// Maßstab des Bilds (ein Maskenpunkt sind zwei Bildpunkte).
    /// VERTRAG: new Rectangle(target.X, target.Y, target.Width * maskWidth * 2 / texWidth,
    /// target.Height * maskHeight * 2 / texHeight).
    /// Reine Rechnung ohne Grafik - tools/spielablauf (Gruppe schatten) prüft sie.
    /// </summary>
    internal static Rectangle ShadowDest(Rectangle target, int texWidth, int texHeight, int maskWidth, int maskHeight)
    {
        // Linke obere Ecke wie das Bild selbst, im Maßstab des Bilds: ein Maskenpunkt
        // sind zwei Bildpunkte.
        return new Rectangle(target.X, target.Y, target.Width * maskWidth * 2 / texWidth,
                             target.Height * maskHeight * 2 / texHeight);
    }

    // Schattenmasken je Bild, Spiegelung, verdeckter Oberkante (Bauphasen) und Bodenkontakt;
    // zu jeder Maske die Bänder fürs Wiegen im Wind (Bäume) - bei Konturen (Gebäude, Haufen)
    // bleiben sie null, die stehen starr.
    private struct ShadowInfo
    {
        public Texture2D Tex;
        public (int Py0, int Py1, float Hoehenteil)[]? SwayBands;
    }
    private readonly Dictionary<(Texture2D Tex, bool Flip, int HiddenTop, bool Contour), ShadowInfo> _shadowMasks = new();

    /// <summary>
    /// Die Schattenmaske eines Bilds als Textur, einmal gerechnet und dann gemerkt; zu ihr
    /// die Bänder fürs Wiegen im Wind (Bäume) mit <see cref="Wind.TREE_STRIPS"/> Streifen.
    /// VERTRAG: Schlüssel (tex, flip, hiddenTop, contour) in _shadowMasks - vorhanden, so
    /// zurückgeben. Sonst: tex.GetData in ein Color-Feld der Größe tex.Width * tex.Height;
    /// alpha = HalfAlpha(Feld, tex.Width, tex.Height, flip, hiddenTop); h2 = (tex.Height + 1) / 2;
    /// mask = BakeShadowMask(alpha, (tex.Width + 1) / 2, h2, contour, out int sw, out int sh);
    /// daraus new Texture2D(graphicsDevice, sw, sh) mit SetData von new Color(mask[i], mask[i],
    /// mask[i], mask[i]) je Punkt (vormultipliziertes Weiß). SwayBands = bei Kontur null,
    /// sonst SwayShadowBands(alpha, (tex.Width + 1) / 2, h2, tex.Height, Wind.TREE_STRIPS).
    /// Merken (Tex, SwayBands), zurückgeben.
    /// </summary>
    private ShadowInfo ShadowMaskFor(Texture2D tex, bool flip, int hiddenTop, bool contour)
    {
        // Gemerkt? Dann wiederverwenden.
        var key = (tex, flip, hiddenTop, contour);
        if (_shadowMasks.TryGetValue(key, out ShadowInfo info))
            return info;

        // Deckkraft in halber Auflösung, daraus die Maske, daraus die Textur.
        Color[] pixels = new Color[tex.Width * tex.Height];
        tex.GetData(pixels, 0, pixels.Length);
        byte[] alpha = HalfAlpha(pixels, tex.Width, tex.Height, flip, hiddenTop);
        int h2 = (tex.Height + 1) / 2;
        int w2 = (tex.Width + 1) / 2;
        byte[] mask = BakeShadowMask(alpha, w2, h2, contour, out int sw, out int sh);

        Texture2D result = new Texture2D(graphicsDevice, sw, sh);
        Color[] colors = new Color[sw * sh];
        for (int i = 0; i < sw * sh; i++)
            colors[i] = new Color(mask[i], mask[i], mask[i], mask[i]);
        result.SetData(colors);

        // Wind (Bäume): die Bänder des Schattens für die Streifen des Baums.
        info = new ShadowInfo
        {
            Tex = result,
            SwayBands = contour ? null : SwayShadowBands(alpha, w2, h2, tex.Height, Wind.TREE_STRIPS),
        };
        _shadowMasks[key] = info;
        return info;
    }

    // Die Masken sammeln sich in einem eigenen Batch, solange DrawTileMap und DrawUnits
    // zeichnen; EndCastShadows legt sie mit Maximum in _shadowTarget und das einmal über
    // den Boden.
    private SpriteBatch _castShadowBatch;
    private RenderTarget2D _shadowTarget;
    private bool _castShadowsOpen;
    private static readonly BlendState ShadowUnion = new BlendState
    {
        ColorSourceBlend = Blend.One, ColorDestinationBlend = Blend.One, ColorBlendFunction = BlendFunction.Max,
        AlphaSourceBlend = Blend.One, AlphaDestinationBlend = Blend.One, AlphaBlendFunction = BlendFunction.Max,
    };

    /// <summary>
    /// Öffnet den Schattenbatch - gleich danach öffnet Draw den Batch der Objekte.
    /// VERTRAG: _castShadowBatch ??= new SpriteBatch(graphicsDevice);
    /// _castShadowBatch.Begin(SpriteSortMode.Deferred, ShadowUnion); _castShadowsOpen = true.
    /// </summary>
    private void BeginCastShadows()
    {
        // Ein eigener Batch sammelt die Masken; Maximum-Blend, damit sich deckende
        // Schatten nicht aufsummieren.
        _castShadowBatch ??= new SpriteBatch(graphicsDevice);
        _castShadowBatch.Begin(SpriteSortMode.Deferred, ShadowUnion);
        _castShadowsOpen = true;
    }

    /// <summary>
    /// Legt alle gesammelten Schatten auf einmal über den Boden - bevor der noch offene
    /// Batch der Objekte gezeichnet wird.
    /// VERTRAG: _castShadowsOpen = false. Ist _shadowTarget null oder nicht so groß wie
    /// graphicsDevice.PresentationParameters.BackBufferWidth × BackBufferHeight, das alte
    /// freigeben (Dispose) und neu anlegen: new RenderTarget2D(graphicsDevice, Breite, Höhe).
    /// var previous = graphicsDevice.GetRenderTargets(); SetRenderTarget(_shadowTarget);
    /// Clear(Color.Transparent); _castShadowBatch.End(); graphicsDevice.SetRenderTargets(previous);
    /// dann _castShadowBatch.Begin() (Standard: vormultipliziertes AlphaBlend), Draw(_shadowTarget,
    /// Vector2.Zero, SHADOW_TINT), End().
    /// </summary>
    private void EndCastShadows()
    {
        _castShadowsOpen = false;

        // Das Schattenbild passt zur Fenstergröße - bei Änderung neu anlegen.
        int width = graphicsDevice.PresentationParameters.BackBufferWidth;
        int height = graphicsDevice.PresentationParameters.BackBufferHeight;
        if (_shadowTarget == null || _shadowTarget.Width != width || _shadowTarget.Height != height)
        {
            _shadowTarget?.Dispose();
            _shadowTarget = new RenderTarget2D(graphicsDevice, width, height);
        }

        // Alle Masken mit Maximum in das Schattenbild legen ...
        var previous = graphicsDevice.GetRenderTargets();
        graphicsDevice.SetRenderTarget(_shadowTarget);
        graphicsDevice.Clear(Color.Transparent);
        _castShadowBatch.End();
        graphicsDevice.SetRenderTargets(previous);

        // ... und das Bild einmal über den Boden legen (vormultipliziertes Alpha).
        _castShadowBatch.Begin();
        _castShadowBatch.Draw(_shadowTarget, Vector2.Zero, SHADOW_TINT);
        _castShadowBatch.End();
    }

    /// <summary>
    /// Der Schlagschatten eines Bilds, das in <paramref name="target"/> mit der Spiegelung
    /// <paramref name="effects"/> gezeichnet wird - aufgerufen direkt vor dem Bild selbst.
    /// <paramref name="contour"/>: Bodenkontakt je Spalte (Gebäude, Haufen) statt am tiefsten
    /// Punkt (Bäume, Figuren, Tiere, Büsche); <paramref name="hiddenTop"/>: so viele Bildzeilen
    /// von oben stehen noch nicht (Bauphasen); <paramref name="density"/>: Anteil des Lichts,
    /// den das Objekt abhält (Laub: TREE_SHADOW_DENSITY).
    /// VERTRAG: mask = ShadowMaskFor(tex, (effects &amp; SpriteEffects.FlipHorizontally) != 0,
    /// hiddenTop, contour); dest = ShadowDest(target, tex.Width, tex.Height, mask.Tex.Width,
    /// mask.Tex.Height). Ist _castShadowsOpen: genau ein _castShadowBatch.Draw(mask, dest,
    /// Color.White * density); sonst (außerhalb des Schattendurchgangs) genau ein
    /// spriteBatch.Draw(mask, dest, SHADOW_TINT * density).
    /// </summary>
    private void DrawCastShadow(SpriteBatch spriteBatch, Texture2D tex, Rectangle target, SpriteEffects effects,
                                bool contour, int hiddenTop = 0, float density = 1f)
    {
        // Maske und Zielrechteck wie das Bild selbst, nur in der Form des Schattens.
        var mask = ShadowMaskFor(tex, (effects & SpriteEffects.FlipHorizontally) != 0, hiddenTop, contour);
        Rectangle dest = ShadowDest(target, tex.Width, tex.Height, mask.Tex.Width, mask.Tex.Height);

        // Im Schattendurchgang sammeln (Maximum-Blend), sonst direkt über den Boden legen.
        if (_castShadowsOpen)
            _castShadowBatch.Draw(mask.Tex, dest, Color.White * density);
        else
            spriteBatch.Draw(mask.Tex, dest, SHADOW_TINT * density);
    }

    /// <summary>
    /// Die Bänder des Schlagschattens für den Winder (Bäume): die Körper-Schattenregion
    /// (selbe Rechnung wie BakeShadowMask mit demselben alpha-Array) wird in so viele
    /// waagerechte Bänder geschnitten wie der Baum Streifen hat (Wind.SwayStrips, gleiche
    /// <paramref name="strips"/> und <paramref name="texHeight"/>), und jedes Band trägt
    /// den gleichen Hoehenteil wie der Streifen des Baums, der es wirft. Zeichnet man
    /// das Band dann um sway * target.Height * Hoehenteil^1,5 nach der Seite, bewegt es
    /// sich genau so wie der Baumstreifen auf gleicher Höhe - der Schatten wiegt sich
    /// wie der Baum.
    /// VERTRAG:
    /// - Eingaben: dasselbe alpha-Array (width × height) wie BakeShadowMask, die volle
    ///   Bildhöhe <paramref name="texHeight"/>, die Streifenanzahl <paramref name="strips"/>.
    /// - Streifen i = 0 … strips-1: Streifenhöhe stripHeight = texHeight / strips,
    ///   oben = i * stripHeight, höhe = texHeight - oben beim letzten Streifen, sonst
    ///   stripHeight (die genaue Geometrie von Wind.SwayStrips); Hoehenteil = 1 -
    ///   (oben + höhe / 2) / texHeight. Streifen i gehört zu den alpha-Zeilen y0 bis
    ///   y1 - 1, y0 = oben / 2, y1 = (oben + höhe + 1) / 2, abgeschnitten auf 0 … height.
    /// - Eine alpha-Zeile deckt, wenn sie einen Bildpunkt mit alpha ≥ 128 hat; lowest =
    ///   unterste deckende Zeile. Ist keine Zeile gedeckt, alle Bänder leer (Py0 == Py1).
    /// - Deckt eine alpha-Zeile y im Streifen, ist ihr Schattenband von cy - ry bis
    ///   cy + ry, cy = lowest + 0,5 + (lowest - y) * SHADOW_FLATTEN und ry wie die
    ///   Halbachse der Zeile in BakeShadowMask (ry = max(0,5, rx * SHADOW_DEPTH)).
    /// - Band i = die Vereinigung der Zeilenbänder seiner deckenden Zeilen, beidseitig
    ///   um SHADOW_SOFT erweitert (der weiche Rand) und abgeschnitten auf das Schattenbild
    ///   (shadowHeight = die Rechnung des BakeShadowMask-Vertrags); leerer Streifen =
    ///   Py0 == Py1.
    /// - Ergebnis: (Py0, Py1, Hoehenteil) je Streifen, in Streifenreihenfolge.
    /// Reine Rechnung ohne Grafik - tools/spielablauf (Gruppe schatten) prüft sie.
    /// </summary>
    internal static (int Py0, int Py1, float Hoehenteil)[] SwayShadowBands(
        byte[] alpha, int width, int height, int texHeight, int strips)
    {
        var baender = new (int Py0, int Py1, float Hoehenteil)[strips];
        int shadowHeight = height + (int)MathF.Ceiling(height * SHADOW_FLATTEN + width * SHADOW_DEPTH / 2f)
                              + 2 * SHADOW_SOFT;
        int stripHeight = texHeight / strips;

        // Unterste deckende Zeile (Stammfuß).
        int lowest = -1;
        for (int y = height - 1; y >= 0; y--)
        {
            bool any = false;
            for (int x = 0; x < width; x++)
            {
                if (alpha[y * width + x] >= 128)
                {
                    any = true;
                    break;
                }
            }
            if (any)
            {
                lowest = y;
                break;
            }
        }

        for (int i = 0; i < strips; i++)
        {
            int oben = i * stripHeight;
            int hoehe = i == strips - 1 ? texHeight - oben : stripHeight;
            float hoehenteil = 1f - (oben + hoehe / 2f) / texHeight;
            int y0 = oben / 2;
            int y1 = (oben + hoehe + 1) / 2;
            if (y0 < 0) y0 = 0;
            if (y1 > height) y1 = height;

            float pyLo = float.MaxValue, pyHi = float.MinValue;
            for (int y = y0; y < y1; y++)
            {
                if (y > lowest)
                    break;
                int left = -1, right = -1;
                for (int x = 0; x < width; x++)
                {
                    if (alpha[y * width + x] >= 128)
                    {
                        if (left < 0)
                            left = x;
                        right = x;
                    }
                }
                if (left < 0)
                    continue;
                float cy = lowest + 0.5f + (lowest - y) * SHADOW_FLATTEN;
                float ry = Math.Max(0.5f, (right - left + 1) / 2f * SHADOW_DEPTH);
                if (cy - ry < pyLo)
                    pyLo = cy - ry;
                if (cy + ry > pyHi)
                    pyHi = cy + ry;
            }
            if (pyLo > pyHi)
            {
                baender[i] = (0, 0, hoehenteil);
                continue;
            }
            int py0 = Math.Max(0, (int)MathF.Floor(pyLo) - SHADOW_SOFT);
            int py1 = Math.Min(shadowHeight, (int)MathF.Ceiling(pyHi) + SHADOW_SOFT);
            baender[i] = (py0, py1, hoehenteil);
        }
        return baender;
    }

    /// <summary>
    /// Der schwingende Schlagschatten eines Bilds (Bäume): statt einer ganzen Maske wird
    /// dieselbe Maske in den Bändern des Baums gezeichnet - jedes Band rückt so zur
    /// Seite wie der Baumstreifen auf seiner Höhe (Wind.SwayStrips, gleicher
    /// <paramref name="sway"/>), weil Hoehenteil und Verschiebung die des Streifens sind.
    /// Die Bänder überlappen um den weichen Rand (SHADOW_SOFT) - im Schattendurchgang
    /// summiert sich das nicht (Maximum-Blend). <paramref name="hiddenTop"/>: so viele
    /// Bildzeilen von oben stehen noch nicht; <paramref name="density"/>: Anteil des
    /// Lichts, den das Objekt abhält.
    /// VERTRAG: info = ShadowMaskFor(tex, (effects &amp; SpriteEffects.FlipHorizontally) != 0,
    /// hiddenTop, false). Ist info.SwayBands null oder <paramref name="sway"/> 0: genau
    /// wie DrawCastShadow (ganze Maske, kein Schwingen). Sonst Zeilenhöhe = target.Height
    /// * 2 / tex.Height (ein Maskenpunkt ist zwei Bildpunkte, wie ShadowDest); für jedes
    /// Band i mit Py0 &lt; Py1: Quelle = (0, Py0, Maske.Breite, Py1 - Py0),
    /// Verschiebung = sway * target.Height * Hoehenteil^1,5, Ziel = (int)(target.X +
    /// Verschiebung) und (int)(target.Y + Py0 * Zeilenhöhe) mit Breite (int)(Maske.Breite
    /// * 2 * target.Width / tex.Width) und Höhe (int)((Py1 - Py0) * Zeilenhöhe). Ist
    /// _castShadowsOpen: genau ein _castShadowBatch.Draw(Maske, Quelle, Ziel, Color.White
    /// * density); sonst genau ein spriteBatch.Draw(Maske, Quelle, Ziel, SHADOW_TINT *
    /// density).
    /// </summary>
    private void DrawSwayCastShadow(SpriteBatch spriteBatch, Texture2D tex, Rectangle target, SpriteEffects effects,
                                    int hiddenTop, float density, float sway)
    {
        var info = ShadowMaskFor(tex, (effects & SpriteEffects.FlipHorizontally) != 0, hiddenTop, false);
        var baender = info.SwayBands;
        if (baender == null)
        {
            // Keine Bänder (Kontur) - wie die ganze Maske.
            var dest = ShadowDest(target, tex.Width, tex.Height, info.Tex.Width, info.Tex.Height);
            if (_castShadowsOpen)
                _castShadowBatch.Draw(info.Tex, dest, Color.White * density);
            else
                spriteBatch.Draw(info.Tex, dest, SHADOW_TINT * density);
            return;
        }

        float zeile = target.Height * 2f / tex.Height;
        float breite = info.Tex.Width * 2f * target.Width / tex.Width;
        for (int i = 0; i < baender.Length; i++)
        {
            var (py0, py1, hoehenteil) = baender[i];
            if (py0 >= py1)
                continue;
            float verschiebung = sway * target.Height * MathF.Pow(hoehenteil, 1.5f);
            var quelle = new Rectangle(0, py0, info.Tex.Width, py1 - py0);
            var ziel = new Rectangle((int)(target.X + verschiebung), (int)(target.Y + py0 * zeile),
                                     (int)breite, (int)((py1 - py0) * zeile));
            if (_castShadowsOpen)
                _castShadowBatch.Draw(info.Tex, quelle, ziel, Color.White * density);
            else
                spriteBatch.Draw(info.Tex, quelle, ziel, SHADOW_TINT * density);
        }
    }

}
