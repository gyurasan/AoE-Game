# Übernimmt die gewählten Bilder aus tools/bilder/ausgabe ins Content-Verzeichnis
# des Spiels: für jeden Eintrag mit "ziel" in bilder.json das Bild mit dem dort
# eingetragenen Seed.
#
#   pwsh -File tools/bilder/uebernehmen.ps1            # alle Gruppen
#   pwsh -File tools/bilder/uebernehmen.ps1 -Gruppe icons
#
# Je Gruppe (oder je Eintrag) steuern diese Felder die Nachbearbeitung:
#   ausschnitt    [x, y, b, h]  vor allem anderen auf dieses Rechteck beschneiden - etwa
#                         Nieten am Bildrand weg, die gekachelt in der Mitte auftauchten
#   zielgroesse   [b, h]  auf genau diese Größe verkleinern (Icons)
#   zuschneiden   true    auf den sichtbaren Bereich (Alpha) beschneiden
#   zielbreite    b       auf diese Breite verkleinern, Höhe im Seitenverhältnis
#   kachelbar     true    nahtlos kachelbar machen: um eine halbe Breite verschieben
#                         (die Außenkanten passen dann), die Naht in der Mitte mit
#                         einem weichen Kreuz aus dem unverschobenen Bild überdecken
#   spielerfarben true    zwei Dateien: <ziel>_blau.png wie erzeugt, <ziel>_rot.png
#                         mit kräftigem Blau (Fahnen, Banner) in Rot umgefärbt
#   blau_saettigung  s    ab welcher Sättigung (0 bis 1, sonst 0,3) Blau als Spielerfarbe
#                         gilt. Die Gebäude der Zeitalter brauchen 0,6: Schieferdächer,
#                         bläuliche Steinschatten und Fenster liegen bei 0,3 bis 0,5
#                         und würden sonst mit rot
#   ausgleichen   a       großflächige Helligkeit ausgleichen (Halbmesser a * Bildbreite):
#                         ein dunkler Fleck in der Mitte eines Bodenbilds würde gekachelt
#                         zum Raster; feine Muster bleiben
#   loecher       true    weiße Löcher im freigestellten Bild (zwischen Ästen und
#                         Blattbüscheln) durchsichtig machen, ohne weißen Saum - für
#                         Bäume mit offener Krone; false je Eintrag bei weißer Rinde
#   figur         name    alle Einträge mit derselben Figur, auch aus anderen Gruppen,
#                         auf dasselbe Rechteck beschneiden: die Vereinigung ihrer
#                         sichtbaren Flächen. So bleiben die Laufbilder eines Tiers
#                         deckungsgleich mit seinem Standbild. Ersetzt "zuschneiden".
#   programmsymbol true   das Symbol der Exe: auf den sichtbaren Bereich beschneiden,
#                         quadratisch machen und als <ziel>.ico mit 16 bis 256 px
#                         schreiben, dazu <ziel>.bmp mit 256 px (Fenstersymbol, MonoGame
#                         lädt es als eingebettete Ressource Icon.bmp) und icon-1024.png
#                         daneben (Vorlage der Symbol-Skripte für macOS)
#
# Neue Ziele muss danach noch jemand in Content/AgeOfEvolutions.mgcb eintragen.
param([string]$Gruppe = "")
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing.Common, System.Drawing.Primitives, System.Private.Windows.GdiPlus, System.Private.Windows.Core -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class Nachbearbeitung
{
    static byte[] Lesen(Bitmap b, out BitmapData d)
    {
        d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        var px = new byte[d.Stride * b.Height];
        System.Runtime.InteropServices.Marshal.Copy(d.Scan0, px, 0, px.Length);
        return px;
    }

    static void Schreiben(Bitmap b, BitmapData d, byte[] px)
    {
        System.Runtime.InteropServices.Marshal.Copy(px, 0, d.Scan0, px.Length);
        b.UnlockBits(d);
    }

    /// Das kleinste Rechteck mit Alpha über der Schwelle; das ganze Bild, wenn nichts sichtbar ist.
    public static Rectangle Sichtbar(Bitmap quelle, byte schwelle)
    {
        var b = new Bitmap(quelle);
        BitmapData d; var px = Lesen(b, out d);
        int l = b.Width, o = b.Height, r = -1, u = -1;
        for (int y = 0; y < b.Height; y++)
            for (int x = 0; x < b.Width; x++)
                if (px[y * d.Stride + x * 4 + 3] > schwelle)
                {
                    l = Math.Min(l, x); r = Math.Max(r, x); o = Math.Min(o, y); u = Math.Max(u, y);
                }
        b.UnlockBits(d);
        b.Dispose();
        return r < 0 ? new Rectangle(0, 0, quelle.Width, quelle.Height) : new Rectangle(l, o, r - l + 1, u - o + 1);
    }

    /// Auf das kleinste Rechteck mit Alpha über der Schwelle beschneiden.
    public static Bitmap Zuschneiden(Bitmap quelle, byte schwelle)
    {
        return Ausschnitt(quelle, Sichtbar(quelle, schwelle));
    }

    /// Auf ein festes Rechteck beschneiden - bei Figuren dasselbe für alle ihre Bilder.
    public static Bitmap Ausschnitt(Bitmap quelle, Rectangle rahmen)
    {
        var b = new Bitmap(quelle);
        var ziel = b.Clone(rahmen, PixelFormat.Format32bppArgb);
        b.Dispose();
        return ziel;
    }

    /// Hochwertig auf die Breite verkleinern, Höhe im Seitenverhältnis; Alpha bleibt.
    public static Bitmap AufBreite(Bitmap quelle, int breite)
    {
        int hoehe = Math.Max(1, (int)Math.Round((double)quelle.Height * breite / quelle.Width));
        var ziel = new Bitmap(breite, hoehe, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(ziel))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using (var a = new ImageAttributes())
            {
                a.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(quelle, new Rectangle(0, 0, breite, hoehe), 0, 0, quelle.Width, quelle.Height, GraphicsUnit.Pixel, a);
            }
        }
        return ziel;
    }

    /// Nahtlos kachelbar: S ist das Bild um eine halbe Breite und Höhe versetzt
    /// (seine Außenkanten stoßen nahtlos aneinander, die Naht liegt im Mittelkreuz).
    /// Ergebnis = S * (1 - m) + Original * m, m = 1 auf dem Mittelkreuz und auf
    /// "breite" Pixel weich auf 0 abfallend - am Rand bleibt S, also kachelbar.
    public static Bitmap Kachelbar(Bitmap quelle, int breite)
    {
        var b = new Bitmap(quelle);
        BitmapData d; var px = Lesen(b, out d);
        int w = b.Width, h = b.Height;
        var neu = new byte[px.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int sx = (x + w / 2) % w, sy = (y + h / 2) % h;
                double mx = Math.Max(0, 1 - Math.Abs(x - w / 2.0) / breite);
                double my = Math.Max(0, 1 - Math.Abs(y - h / 2.0) / breite);
                double m = Math.Max(mx * mx * (3 - 2 * mx), my * my * (3 - 2 * my));
                int i = y * d.Stride + x * 4, j = sy * d.Stride + sx * 4;
                for (int k = 0; k < 4; k++)
                    neu[i + k] = (byte)Math.Round(px[j + k] * (1 - m) + px[i + k] * m);
            }
        Schreiben(b, d, neu);
        return b;
    }

    /// Waagerecht, dann senkrecht: Minimum (erodieren) oder Maximum (dehnen) eines
    /// Ja/Nein-Felds über ein Quadrat mit Halbmesser r.
    static bool[] Filter(bool[] feld, int w, int h, int r, bool dehnen)
    {
        var zwischen = new bool[feld.Length];
        var aus = new bool[feld.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool v = !dehnen;
                for (int k = Math.Max(0, x - r); k <= Math.Min(w - 1, x + r); k++)
                    if (feld[y * w + k] == dehnen) { v = dehnen; break; }
                zwischen[y * w + x] = v;
            }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool v = !dehnen;
                for (int k = Math.Max(0, y - r); k <= Math.Min(h - 1, y + r); k++)
                    if (zwischen[k * w + x] == dehnen) { v = dehnen; break; }
                aus[y * w + x] = v;
            }
        return aus;
    }

    /// Weiße Löcher in einem freigestellten Bild durchsichtig machen: BiRefNet
    /// stellt nur den Umriss frei, zwischen Ästen und Blattbüscheln bleibt der
    /// weiße Hintergrund stehen. Weiß sind Pixel mit min(R,G,B) >= 232 und wenig
    /// Sättigung; Flecken kleiner als 5x5 Pixel (Glanzlichter) fallen per Öffnen
    /// heraus. In den Löchern und "rand" Pixel darum herum wird Weiß wie bei
    /// "Farbe zu Alpha" herausgerechnet: a = größter Abstand eines Kanals zu 255,
    /// die Farbe wird entmischt (kein weißer Saum), und alles ab a = 0,45 bleibt
    /// voll deckend. Das Alpha von BiRefNet wird dabei nur verringert, nie erhöht.
    public static Bitmap WeisseLoecher(Bitmap quelle, int rand)
    {
        var b = new Bitmap(quelle);
        BitmapData d; var px = Lesen(b, out d);
        int w = b.Width, h = b.Height;
        var weiss = new bool[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * d.Stride + x * 4;
                int lo = Math.Min(px[i], Math.Min(px[i + 1], px[i + 2]));
                int hi = Math.Max(px[i], Math.Max(px[i + 1], px[i + 2]));
                weiss[y * w + x] = lo >= 232 && hi - lo <= 24;
            }
        var loch = Filter(Filter(weiss, w, h, 2, false), w, h, 2, true);
        var zone = Filter(loch, w, h, rand, true);
        // Einzelne weiße Glanzpunkte, die kein Loch sind, bekommen die Farbe ihrer nicht
        // weißen Nachbarn - im Laub wirken sie wie Fehler
        for (int y = 1; y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
            {
                if (!weiss[y * w + x] || zone[y * w + x]) continue;
                int i = y * d.Stride + x * 4, n = 0, sb = 0, sg = 0, sr = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (weiss[(y + dy) * w + x + dx]) continue;
                        int j = (y + dy) * d.Stride + (x + dx) * 4;
                        sb += px[j]; sg += px[j + 1]; sr += px[j + 2]; n++;
                    }
                if (n == 0) continue;
                px[i] = (byte)(sb / n); px[i + 1] = (byte)(sg / n); px[i + 2] = (byte)(sr / n);
            }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!zone[y * w + x]) continue;
                int i = y * d.Stride + x * 4;
                double a = 0;
                for (int k = 0; k < 3; k++) a = Math.Max(a, (255 - px[i + k]) / 255.0);
                double t = Math.Min(1.0, a / 0.45);
                double neu = t * t * (3 - 2 * t);
                if (a > 0.001)
                    for (int k = 0; k < 3; k++)
                        px[i + k] = (byte)Math.Round(Math.Max(0, Math.Min(255, 255 - (255 - px[i + k]) / a)));
                px[i + 3] = (byte)Math.Min(px[i + 3], (int)Math.Round(neu * 255));
            }
        Schreiben(b, d, px);
        return b;
    }

    /// Großflächige Helligkeit und Farbe ausgleichen, je Farbkanal: jeder Pixel wird
    /// mit mittel / Umgebung gestreckt, Umgebung = Mittelwert des Kanals im Quadrat
    /// mit Halbmesser r (zweimal Kastenfilter, am Rand gespiegelt), mittel = Mittel
    /// über das ganze Bild. Feine Muster (Halme, Klee, Maserung) bleiben; ein dunkler
    /// Fleck in der Bildmitte, helle Ränder oder ein Farbverlauf (gelbe Mitte, orange
    /// Ränder) verschwinden - sonst zeigt das gekachelte Bild ein Raster oder an den
    /// Überblendungen der Kachelnaht schlammige Mischfarben.
    public static Bitmap Ausgleichen(Bitmap quelle, int r)
    {
        var b = new Bitmap(quelle);
        BitmapData d; var px = Lesen(b, out d);
        int w = b.Width, h = b.Height;
        for (int k = 0; k < 3; k++)
        {
            var kanal = new double[w * h];
            double summe = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    kanal[y * w + x] = px[y * d.Stride + x * 4 + k];
                    summe += kanal[y * w + x];
                }
            double mittel = summe / (w * h);
            var glatt = kanal;
            for (int lauf = 0; lauf < 2; lauf++)
                glatt = Kasten(Kasten(glatt, w, h, r, true), w, h, r, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * d.Stride + x * 4 + k;
                    double f = mittel / Math.Max(1.0, glatt[y * w + x]);
                    px[i] = (byte)Math.Max(0, Math.Min(255, Math.Round(px[i] * f)));
                }
        }
        Schreiben(b, d, px);
        return b;
    }

    /// Kastenfilter mit Halbmesser r waagerecht oder senkrecht, am Rand gespiegelt.
    static double[] Kasten(double[] feld, int w, int h, int r, bool waagerecht)
    {
        var aus = new double[feld.Length];
        int n = waagerecht ? w : h, m = waagerecht ? h : w;
        for (int j = 0; j < m; j++)
        {
            Func<int, double> wert = k =>
            {
                if (k < 0) k = -k - 1;
                if (k >= n) k = 2 * n - k - 1;
                k = Math.Max(0, Math.Min(n - 1, k));
                return waagerecht ? feld[j * w + k] : feld[k * w + j];
            };
            double s = 0;
            for (int k = -r; k <= r; k++) s += wert(k);
            for (int k = 0; k < n; k++)
            {
                if (waagerecht) aus[j * w + k] = s / (2 * r + 1); else aus[k * w + j] = s / (2 * r + 1);
                s += wert(k + r + 1) - wert(k - r);
            }
        }
        return aus;
    }

    /// Kräftiges Blau (Farbton 190-260 Grad, Sättigung ab saettigung) wird Rot:
    /// der Bereich wird um 225 Grad verschoben und gestaucht, Helligkeit bleibt.
    public static Bitmap BlauZuRot(Bitmap quelle, double saettigung)
    {
        var b = new Bitmap(quelle);
        BitmapData d; var px = Lesen(b, out d);
        for (int i = 0; i < px.Length; i += 4)
        {
            if (px[i + 3] == 0) continue;
            double bl = px[i] / 255.0, gr = px[i + 1] / 255.0, rt = px[i + 2] / 255.0;
            double max = Math.Max(rt, Math.Max(gr, bl)), min = Math.Min(rt, Math.Min(gr, bl)), c = max - min;
            if (c <= 0 || max < 0.15) continue;
            double s = c / max;
            double h = max == rt ? 60 * (((gr - bl) / c) % 6) : max == gr ? 60 * ((bl - rt) / c + 2) : 60 * ((rt - gr) / c + 4);
            if (h < 0) h += 360;
            if (h < 190 || h > 260 || s < saettigung) continue;
            double neu = ((h - 225) * 0.4 + 360) % 360;
            double x = c * (1 - Math.Abs((neu / 60) % 2 - 1)), m = max - c;
            double r1 = 0, g1 = 0, b1 = 0;
            if (neu < 60) { r1 = c; g1 = x; } else if (neu < 120) { r1 = x; g1 = c; }
            else if (neu < 180) { g1 = c; b1 = x; } else if (neu < 240) { g1 = x; b1 = c; }
            else if (neu < 300) { r1 = x; b1 = c; } else { r1 = c; b1 = x; }
            px[i] = (byte)Math.Round((b1 + m) * 255); px[i + 1] = (byte)Math.Round((g1 + m) * 255);
            px[i + 2] = (byte)Math.Round((r1 + m) * 255);
        }
        Schreiben(b, d, px);
        return b;
    }

    /// Auf den sichtbaren Bereich beschneiden und mittig auf ein durchsichtiges
    /// Quadrat setzen, das ringsum "rand" (Anteil der Kante) Luft lässt.
    public static Bitmap Quadratisch(Bitmap quelle, byte schwelle, double rand)
    {
        var r = Sichtbar(quelle, schwelle);
        int kante = (int)Math.Ceiling(Math.Max(r.Width, r.Height) / (1 - 2 * rand));
        var ziel = new Bitmap(kante, kante, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(ziel))
        {
            g.Clear(Color.Transparent);
            g.DrawImage(quelle, new Rectangle((kante - r.Width) / 2, (kante - r.Height) / 2, r.Width, r.Height),
                r, GraphicsUnit.Pixel);
        }
        return ziel;
    }

    /// Hochwertig auf kante x kante verkleinern; Alpha bleibt.
    public static Bitmap Quadrat(Bitmap quelle, int kante)
    {
        var ziel = new Bitmap(kante, kante, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(ziel))
        using (var a = new ImageAttributes())
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            a.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(quelle, new Rectangle(0, 0, kante, kante), 0, 0, quelle.Width, quelle.Height, GraphicsUnit.Pixel, a);
        }
        return ziel;
    }

    /// Die Pixel von unten nach oben als BGRA, wie DIBs sie erwarten.
    static byte[] VonUnten(Bitmap b)
    {
        BitmapData d; var px = Lesen(b, out d);
        b.UnlockBits(d);
        int zeile = b.Width * 4;
        var aus = new byte[zeile * b.Height];
        for (int y = 0; y < b.Height; y++)
            Array.Copy(px, y * d.Stride, aus, (b.Height - 1 - y) * zeile, zeile);
        return aus;
    }

    /// Windows-Symbol: bis 128 px als 32-Bit-DIB mit UND-Maske, 256 px als PNG -
    /// dieselbe Aufteilung wie das Icon.ico der MonoGame-Vorlage.
    public static void IcoSpeichern(Bitmap quelle, string ziel, int[] groessen)
    {
        var bilder = new byte[groessen.Length][];
        for (int i = 0; i < groessen.Length; i++)
            using (var b = Quadrat(quelle, groessen[i]))
            using (var m = new System.IO.MemoryStream())
            using (var w = new System.IO.BinaryWriter(m))
            {
                int n = groessen[i];
                if (n >= 256) b.Save(m, ImageFormat.Png);
                else
                {
                    var px = VonUnten(b);
                    int maskenzeile = (n + 31) / 32 * 4;
                    var maske = new byte[maskenzeile * n];
                    for (int y = 0; y < n; y++)
                        for (int x = 0; x < n; x++)
                            if (px[(y * n + x) * 4 + 3] == 0) maske[y * maskenzeile + x / 8] |= (byte)(0x80 >> (x % 8));
                    w.Write(40); w.Write(n); w.Write(2 * n); w.Write((short)1); w.Write((short)32);
                    w.Write(0); w.Write(px.Length + maske.Length); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
                    w.Write(px); w.Write(maske);
                }
                w.Flush();
                bilder[i] = m.ToArray();
            }
        using (var f = new System.IO.BinaryWriter(System.IO.File.Create(ziel)))
        {
            f.Write((short)0); f.Write((short)1); f.Write((short)groessen.Length);
            int versatz = 6 + 16 * groessen.Length;
            for (int i = 0; i < groessen.Length; i++)
            {
                byte k = (byte)(groessen[i] >= 256 ? 0 : groessen[i]);
                f.Write(k); f.Write(k); f.Write((byte)0); f.Write((byte)0);
                f.Write((short)1); f.Write((short)32); f.Write(bilder[i].Length); f.Write(versatz);
                versatz += bilder[i].Length;
            }
            foreach (var daten in bilder) f.Write(daten);
        }
    }

    /// 32-Bit-BMP mit BITMAPV5HEADER und Alphamaske, wie das Icon.bmp der Vorlage:
    /// Bitmap.Save schriebe ein BMP ohne Alpha, SDL zeigte das Symbol dann eckig.
    public static void BmpSpeichern(Bitmap quelle, string ziel)
    {
        var px = VonUnten(quelle);
        using (var f = new System.IO.BinaryWriter(System.IO.File.Create(ziel)))
        {
            f.Write((byte)'B'); f.Write((byte)'M'); f.Write(14 + 124 + px.Length); f.Write(0); f.Write(14 + 124);
            f.Write(124); f.Write(quelle.Width); f.Write(quelle.Height); f.Write((short)1); f.Write((short)32);
            f.Write(3); f.Write(px.Length); f.Write(2835); f.Write(2835); f.Write(0); f.Write(0);
            f.Write(0x00FF0000); f.Write(0x0000FF00); f.Write(0x000000FF); f.Write(unchecked((int)0xFF000000));
            f.Write(0x73524742);                      // LCS_sRGB
            f.Write(new byte[36 + 12]);               // Farbraum-Endpunkte und Gamma: bei sRGB ungenutzt
            f.Write(4); f.Write(0); f.Write(0); f.Write(0);   // LCS_GM_IMAGES, kein Profil
            f.Write(px);
        }
    }
}
"@

$hier = $PSScriptRoot
$content = Join-Path $hier "..\..\AgeOfEvolutions\AgeOfEvolutions.Core\Content" | Resolve-Path
$katalog = Get-Content (Join-Path $hier "bilder.json") -Raw -Encoding utf8 | ConvertFrom-Json

function Wert($eintrag, $gruppe, [string]$feld) {
    if ($null -ne $eintrag.$feld) { return $eintrag.$feld }
    return $gruppe.$feld
}

# Figuren: das gemeinsame Rechteck aller Einträge einer Figur, über alle Gruppen -
# auch wenn nur eine Gruppe übernommen wird, sonst passten die Bilder nicht zueinander
$figuren = @{}
foreach ($g in $katalog.PSObject.Properties) {
    foreach ($e in $g.Value.bilder) {
        if (-not $e.figur -or -not $e.ziel) { continue }
        $quelle = Join-Path $hier "ausgabe\$($g.Name)\$($e.name)_$($e.seed).png"
        if (-not (Test-Path $quelle)) { continue }
        $b = [System.Drawing.Bitmap]::new($quelle)
        try { $r = [Nachbearbeitung]::Sichtbar($b, 8) } finally { $b.Dispose() }
        if ($figuren.ContainsKey($e.figur)) { $r = [System.Drawing.Rectangle]::Union($figuren[$e.figur], $r) }
        $figuren[$e.figur] = $r
    }
}

foreach ($g in $katalog.PSObject.Properties) {
    if ($Gruppe -and $g.Name -ne $Gruppe) { continue }
    foreach ($e in $g.Value.bilder) {
        if (-not $e.ziel) { continue }
        $quelle = Join-Path $hier "ausgabe\$($g.Name)\$($e.name)_$($e.seed).png"
        if (-not (Test-Path $quelle)) { throw "fehlt: $quelle - erst qwen_image.py $($g.Name) laufen lassen" }
        $ziel = Join-Path $content $e.ziel
        New-Item -ItemType Directory -Force (Split-Path $ziel) | Out-Null

        $bild = [System.Drawing.Bitmap]::new($quelle)
        try {
            if (Wert $e $g.Value "programmsymbol") {
                $basis = [System.IO.Path]::ChangeExtension($ziel, $null).TrimEnd('.')
                $quadrat = [Nachbearbeitung]::Quadratisch($bild, 8, 0.02)
                try {
                    [Nachbearbeitung]::IcoSpeichern($quadrat, "$basis.ico", [int[]](16, 20, 24, 32, 40, 48, 64, 128, 256))
                    $klein = [Nachbearbeitung]::Quadrat($quadrat, 256)
                    [Nachbearbeitung]::BmpSpeichern($klein, "$basis.bmp"); $klein.Dispose()
                    $gross = [Nachbearbeitung]::Quadrat($quadrat, 1024)
                    $gross.Save((Join-Path (Split-Path $ziel) "icon-1024.png"), [System.Drawing.Imaging.ImageFormat]::Png)
                    $gross.Dispose()
                } finally { $quadrat.Dispose() }
                Write-Output ("{0,-34} -> {1} (ico 16-256, bmp 256, icon-1024.png)" -f (Split-Path $quelle -Leaf), $e.ziel)
                continue
            }
            $rahmen = Wert $e $g.Value "ausschnitt"
            if ($rahmen) {
                $r = [System.Drawing.Rectangle]::new([int]$rahmen[0], [int]$rahmen[1], [int]$rahmen[2], [int]$rahmen[3])
                $neu = [Nachbearbeitung]::Ausschnitt($bild, $r); $bild.Dispose(); $bild = $neu
            }
            $groesse = Wert $e $g.Value "zielgroesse"
            if ($groesse) {
                $w = [int]$groesse[0]; $h = [int]$groesse[1]
                $klein = [System.Drawing.Bitmap]::new($w, $h)
                $gr = [System.Drawing.Graphics]::FromImage($klein)
                $gr.InterpolationMode = 'HighQualityBicubic'
                $gr.PixelOffsetMode = 'HighQuality'
                $gr.DrawImage($bild, 0, 0, $w, $h)
                $gr.Dispose()
                $bild.Dispose(); $bild = $klein
            }
            $ausgleich = Wert $e $g.Value "ausgleichen"
            if ($ausgleich) {
                $neu = [Nachbearbeitung]::Ausgleichen($bild, [int]($bild.Width * [double]$ausgleich)); $bild.Dispose(); $bild = $neu
            }
            if (Wert $e $g.Value "kachelbar") {
                $neu = [Nachbearbeitung]::Kachelbar($bild, [int]($bild.Width / 8)); $bild.Dispose(); $bild = $neu
            }
            if (Wert $e $g.Value "loecher") {
                $neu = [Nachbearbeitung]::WeisseLoecher($bild, 3); $bild.Dispose(); $bild = $neu
            }
            if ($e.figur -and $figuren.ContainsKey($e.figur)) {
                $neu = [Nachbearbeitung]::Ausschnitt($bild, $figuren[$e.figur]); $bild.Dispose(); $bild = $neu
            } elseif (Wert $e $g.Value "zuschneiden") {
                $neu = [Nachbearbeitung]::Zuschneiden($bild, 8); $bild.Dispose(); $bild = $neu
            }
            $breite = Wert $e $g.Value "zielbreite"
            if ($breite) {
                $neu = [Nachbearbeitung]::AufBreite($bild, [int]$breite); $bild.Dispose(); $bild = $neu
            }
            if (Wert $e $g.Value "spielerfarben") {
                $basis = [System.IO.Path]::ChangeExtension($ziel, $null).TrimEnd('.')
                $bild.Save("${basis}_blau.png", [System.Drawing.Imaging.ImageFormat]::Png)
                $saettigung = Wert $e $g.Value "blau_saettigung"
                if ($null -eq $saettigung) { $saettigung = 0.3 }
                $rot = [Nachbearbeitung]::BlauZuRot($bild, [double]$saettigung)
                $rot.Save("${basis}_rot.png", [System.Drawing.Imaging.ImageFormat]::Png); $rot.Dispose()
                Write-Output ("{0,-34} -> {1} (blau, rot; {2}x{3})" -f (Split-Path $quelle -Leaf), $e.ziel, $bild.Width, $bild.Height)
            } else {
                $bild.Save($ziel, [System.Drawing.Imaging.ImageFormat]::Png)
                Write-Output ("{0,-34} -> {1}" -f (Split-Path $quelle -Leaf), $e.ziel)
            }
        } finally { $bild.Dispose() }
    }
}
