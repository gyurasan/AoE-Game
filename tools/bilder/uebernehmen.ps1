# Übernimmt die gewählten Bilder aus tools/bilder/ausgabe ins Content-Verzeichnis
# des Spiels: für jeden Eintrag mit "ziel" in bilder.json das Bild mit dem dort
# eingetragenen Seed.
#
#   pwsh -File tools/bilder/uebernehmen.ps1            # alle Gruppen
#   pwsh -File tools/bilder/uebernehmen.ps1 -Gruppe icons
#
# Je Gruppe (oder je Eintrag) steuern diese Felder die Nachbearbeitung:
#   zielgroesse   [b, h]  auf genau diese Größe verkleinern (Icons)
#   zuschneiden   true    auf den sichtbaren Bereich (Alpha) beschneiden
#   zielbreite    b       auf diese Breite verkleinern, Höhe im Seitenverhältnis
#   kachelbar     true    nahtlos kachelbar machen: um eine halbe Breite verschieben
#                         (die Außenkanten passen dann), die Naht in der Mitte mit
#                         einem weichen Kreuz aus dem unverschobenen Bild überdecken
#   spielerfarben true    zwei Dateien: <ziel>_blau.png wie erzeugt, <ziel>_rot.png
#                         mit kräftigem Blau (Fahnen, Banner) in Rot umgefärbt
#   figur         name    alle Einträge mit derselben Figur, auch aus anderen Gruppen,
#                         auf dasselbe Rechteck beschneiden: die Vereinigung ihrer
#                         sichtbaren Flächen. So bleiben die Laufbilder eines Tiers
#                         deckungsgleich mit seinem Standbild. Ersetzt "zuschneiden".
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

    /// Kräftiges Blau (Farbton 190-260 Grad, Sättigung ab 0,3) wird Rot: der
    /// Bereich wird um 225 Grad verschoben und gestaucht, Helligkeit bleibt.
    public static Bitmap BlauZuRot(Bitmap quelle)
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
            if (h < 190 || h > 260 || s < 0.3) continue;
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
            if (Wert $e $g.Value "kachelbar") {
                $neu = [Nachbearbeitung]::Kachelbar($bild, [int]($bild.Width / 8)); $bild.Dispose(); $bild = $neu
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
                $rot = [Nachbearbeitung]::BlauZuRot($bild)
                $rot.Save("${basis}_rot.png", [System.Drawing.Imaging.ImageFormat]::Png); $rot.Dispose()
                Write-Output ("{0,-34} -> {1} (blau, rot; {2}x{3})" -f (Split-Path $quelle -Leaf), $e.ziel, $bild.Width, $bild.Height)
            } else {
                $bild.Save($ziel, [System.Drawing.Imaging.ImageFormat]::Png)
                Write-Output ("{0,-34} -> {1}" -f (Split-Path $quelle -Leaf), $e.ziel)
            }
        } finally { $bild.Dispose() }
    }
}
