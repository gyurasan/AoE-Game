# Übernimmt die gewählten Bilder aus tools/bilder/ausgabe ins Content-Verzeichnis
# des Spiels: für jeden Eintrag mit "ziel" in bilder.json das Bild mit dem dort
# eingetragenen Seed, bei "zielgroesse" hochwertig verkleinert.
#
#   pwsh -File tools/bilder/uebernehmen.ps1            # alle Gruppen
#   pwsh -File tools/bilder/uebernehmen.ps1 -Gruppe icons
#
# Neue Ziele muss danach noch jemand in Content/AgeOfEvolutions.mgcb eintragen.
param([string]$Gruppe = "")
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$hier = $PSScriptRoot
$content = Join-Path $hier "..\..\AgeOfEvolutions\AgeOfEvolutions.Core\Content" | Resolve-Path
$katalog = Get-Content (Join-Path $hier "bilder.json") -Raw -Encoding utf8 | ConvertFrom-Json

foreach ($g in $katalog.PSObject.Properties) {
    if ($Gruppe -and $g.Name -ne $Gruppe) { continue }
    foreach ($e in $g.Value.bilder) {
        if (-not $e.ziel) { continue }
        $quelle = Join-Path $hier "ausgabe\$($g.Name)\$($e.name)_$($e.seed).png"
        if (-not (Test-Path $quelle)) { throw "fehlt: $quelle - erst qwen_image.py $($g.Name) laufen lassen" }
        $ziel = Join-Path $content $e.ziel
        New-Item -ItemType Directory -Force (Split-Path $ziel) | Out-Null

        $bild = [System.Drawing.Image]::FromFile($quelle)
        try {
            if ($g.Value.zielgroesse) {
                $w = [int]$g.Value.zielgroesse[0]; $h = [int]$g.Value.zielgroesse[1]
                $klein = [System.Drawing.Bitmap]::new($w, $h)
                $gr = [System.Drawing.Graphics]::FromImage($klein)
                $gr.InterpolationMode = 'HighQualityBicubic'
                $gr.PixelOffsetMode = 'HighQuality'
                $gr.DrawImage($bild, 0, 0, $w, $h)
                $gr.Dispose()
                $klein.Save($ziel, [System.Drawing.Imaging.ImageFormat]::Png)
                $klein.Dispose()
            } else {
                $bild.Save($ziel, [System.Drawing.Imaging.ImageFormat]::Png)
            }
        } finally { $bild.Dispose() }
        Write-Output ("{0,-34} -> {1}" -f (Split-Path $quelle -Leaf), $e.ziel)
    }
}
