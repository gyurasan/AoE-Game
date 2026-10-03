# Startet das Spiel, wartet, fotografiert die Fensterflaeche (ohne Rahmen und
# Titelleiste) als BMP und beendet das Spiel wieder. Fuer Abnahmen, die sehen
# muessen, was tatsaechlich gezeichnet wird.
#
#   pwsh -File _fenster.ps1 -Exe <spiel.exe> -Out <bild.bmp> [-GameArgs --rts] [-Wait 8]
#
# Der Thread rechnet DPI-unbewusst: Fenstergroesse, Lage und Bild kommen dann in
# denselben logischen Pixeln wie die Spielgrafik, auch bei 200 % Skalierung.
# BMP statt PNG, weil Python es ohne Pillow lesen kann.
param(
    [Parameter(Mandatory)][string]$Exe,
    [Parameter(Mandatory)][string]$Out,
    [string]$GameArgs = "--rts",
    [int]$Wait = 8
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Fenster {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr v);
}
"@
[Fenster]::SetThreadDpiAwarenessContext([IntPtr](-1)) | Out-Null   # DPI_AWARENESS_CONTEXT_UNAWARE

$p = Start-Process -FilePath $Exe -ArgumentList $GameArgs -WorkingDirectory (Split-Path $Exe) -PassThru
try {
    Start-Sleep -Seconds $Wait
    $p.Refresh()
    if ($p.HasExited) { Write-Output "BEENDET exit $($p.ExitCode)"; exit 2 }
    $h = $p.MainWindowHandle
    if ($h -eq [IntPtr]::Zero) { Write-Output "KEIN FENSTER"; exit 2 }

    $w = New-Object Fenster+RECT; [Fenster]::GetWindowRect($h, [ref]$w) | Out-Null
    $c = New-Object Fenster+RECT; [Fenster]::GetClientRect($h, [ref]$c) | Out-Null
    $o = New-Object Fenster+POINT; [Fenster]::ClientToScreen($h, [ref]$o) | Out-Null

    $voll = New-Object System.Drawing.Bitmap ($w.R - $w.L), ($w.B - $w.T)
    $g = [System.Drawing.Graphics]::FromImage($voll)
    $hdc = $g.GetHdc()
    $ok = [Fenster]::PrintWindow($h, $hdc, 2)                      # PW_RENDERFULLCONTENT
    $g.ReleaseHdc($hdc); $g.Dispose()
    if (-not $ok) { Write-Output "PRINTWINDOW FEHLGESCHLAGEN"; exit 2 }

    $flaeche = New-Object System.Drawing.Rectangle ($o.X - $w.L), ($o.Y - $w.T), $c.R, $c.B
    $bild = $voll.Clone($flaeche, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $bild.Save($Out, [System.Drawing.Imaging.ImageFormat]::Bmp)
    Write-Output "OK $($c.R)x$($c.B)"
}
finally {
    if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
}
