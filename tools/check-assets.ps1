Add-Type -AssemblyName System.Drawing
$assets = @('ResizeMe\\Assets\\StoreLogo.png', 'ResizeMe\\Assets\\Square44x44Logo.scale-200.png', 'ResizeMe\\Assets\\Square150x150Logo.scale-200.png', 'ResizeMe\\Assets\\Wide310x150Logo.scale-200.png', 'ResizeMe\\Assets\\SplashScreen.scale-200.png')
foreach ($p in $assets) {
    if (Test-Path $p) {
        $img = [System.Drawing.Image]::FromFile($p)
        $len = (Get-Item $p).Length
        Write-Host "$p : $($img.Width)x$($img.Height) | $len bytes"
        $img.Dispose()
    }
    else {
        Write-Host "$p : MISSING"
    }
}