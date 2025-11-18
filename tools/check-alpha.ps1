Add-Type -AssemblyName System.Drawing
$paths = @( 'ResizeMe\\Assets\\StoreLogo.png', 'ResizeMe\\Assets\\Square44x44Logo.scale-200.png', 'ResizeMe\\Assets\\Square150x150Logo.scale-200.png', 'ResizeMe\\Assets\\Wide310x150Logo.scale-200.png', 'ResizeMe\\Assets\\SplashScreen.scale-200.png')
foreach ($p in $paths) {
    if (Test-Path $p) {
        $bmp = New-Object System.Drawing.Bitmap($p)
        $transparent = $false
        for ($x = 0; $x -lt [Math]::Min(10,$bmp.Width); $x++) {
            for ($y = 0; $y -lt [Math]::Min(10,$bmp.Height); $y++) {
                $c = $bmp.GetPixel($x,$y)
                if ($c.A -lt 255) { $transparent = $true; break }
            }
            if ($transparent) { break }
        }
        Write-Host "$p : AlphaPresent=$transparent"
        $bmp.Dispose()
    } else { Write-Host "$p : MISSING" }
}