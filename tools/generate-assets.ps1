param(
    [Parameter(Mandatory=$true)]
    [string]$Source,
    [string]$OldDir = "ResizeMe\Assets\old",
    [string]$DestDir = "ResizeMe\Assets"
)

function Get-ImageSize {
    param([string]$Path)
    # Prefer ImageMagick if available
    if (Get-Command magick -ErrorAction SilentlyContinue) {
        $out = magick identify -format "%w,%h" -- "${Path}"
        if ($out) { $parts = $out -split ','; return @{Width=[int]$parts[0]; Height=[int]$parts[1]} }
    }

    # Try WPF BitmapDecoder (available on Windows)
    try {
        Add-Type -AssemblyName PresentationCore -ErrorAction Stop
        $stream = [System.IO.File]::OpenRead($Path)
        $decoder = [System.Windows.Media.Imaging.BitmapDecoder]::Create($stream, [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat, [System.Windows.Media.Imaging.BitmapCacheOption]::Default)
        $frame = $decoder.Frames[0]
        $w = $frame.PixelWidth
        $h = $frame.PixelHeight
        $stream.Close()
        return @{Width=$w; Height=$h}
    }
    catch {
        # Try System.Drawing as fallback
        try {
            Add-Type -AssemblyName System.Drawing -ErrorAction Stop
            $img = [System.Drawing.Image]::FromFile($Path)
            $w = $img.Width
            $h = $img.Height
            $img.Dispose()
            return @{Width=$w; Height=$h}
        }
        catch {
            Write-Error "Unable to determine image size for $Path. Install ImageMagick or run in Windows PowerShell with PresentationCore available."
            return $null
        }
    }
}

function Resize-WithMagick {
    param($Source, $Width, $Height, $Dest)
    # Fit preserving aspect ratio and then pad to exact size with transparent background
    magick convert -- "$Source" -resize ${Width}x${Height}^ -gravity center -extent ${Width}x${Height} -background transparent "$Dest"
}

function Resize-WithWpf {
    param($Source, $Width, $Height, $Dest)

    Add-Type -AssemblyName PresentationCore -ErrorAction Stop
    Add-Type -AssemblyName WindowsBase -ErrorAction Stop

    $bitmap = New-Object System.Windows.Media.Imaging.BitmapImage
    $fs = [System.IO.File]::OpenRead($Source)
    try {
        $bitmap.BeginInit()
        $bitmap.CacheOption = [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
        $bitmap.StreamSource = $fs
        $bitmap.EndInit()
        $bitmap.Freeze()
    }
    finally { $fs.Close() }

    $srcW = $bitmap.PixelWidth
    $srcH = $bitmap.PixelHeight

    $scale = [Math]::Min($Width / $srcW, $Height / $srcH)
    $scaledW = [int]([Math]::Round($srcW * $scale))
    $scaledH = [int]([Math]::Round($srcH * $scale))

    $drawingVisual = New-Object System.Windows.Media.DrawingVisual
    $dc = $drawingVisual.RenderOpen()

    # Transparent background
    $rect = New-Object System.Windows.Rect(0,0,$Width,$Height)
    $dc.DrawRectangle([System.Windows.Media.Brushes]::Transparent, $null, $rect)

    $offsetX = ([Math]::Round(($Width - $scaledW) / 2.0))
    $offsetY = ([Math]::Round(($Height - $scaledH) / 2.0))

    $srcRect = New-Object System.Windows.Rect(0,0,$scaledW,$scaledH)
    $destRect = New-Object System.Windows.Rect($offsetX, $offsetY, $scaledW, $scaledH)

    $imageBrush = New-Object System.Windows.Media.ImageBrush($bitmap)
    $imageBrush.Stretch = [System.Windows.Media.Stretch]::Fill
    $imageBrush.Viewbox = $srcRect
    $imageBrush.ViewboxUnits = [System.Windows.Media.BrushMappingMode]::Absolute

    $dc.DrawRectangle($imageBrush, $null, $destRect)
    $dc.Close()

    $rt = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($Width, $Height, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $rt.Render($drawingVisual)

    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($rt))

    $fsOut = [System.IO.File]::Open($Dest, [System.IO.FileMode]::Create)
    try { $encoder.Save($fsOut) } finally { $fsOut.Close() }
}

# Resolve paths
$srcPath = Resolve-Path -LiteralPath $Source -ErrorAction Stop | Select-Object -ExpandProperty Path
$oldPath = Resolve-Path -LiteralPath $OldDir -ErrorAction Stop | Select-Object -ExpandProperty Path
$destPath = Resolve-Path -LiteralPath $DestDir -ErrorAction SilentlyContinue
if (-not $destPath) { New-Item -ItemType Directory -Path $DestDir -Force | Out-Null; $destPath = Resolve-Path -LiteralPath $DestDir | Select-Object -ExpandProperty Path }

$useMagick = (Get-Command magick -ErrorAction SilentlyContinue) -ne $null

$files = Get-ChildItem -Path $oldPath -File | Where-Object { $_.Extension -match '\.png|\.jpg|\.jpeg|\.gif' }
if ($files.Count -eq 0) { Write-Error "No image files found in $oldPath"; exit 1 }

foreach ($f in $files) {
    $size = Get-ImageSize -Path $f.FullName
    if (-not $size) { continue }
    $w = $size.Width
    $h = $size.Height

    $outFile = Join-Path $destPath $f.Name
    Write-Host "Generating $outFile -> ${w}x${h}"

    if ($useMagick) {
        Resize-WithMagick -Source $srcPath -Width $w -Height $h -Dest $outFile
    }
    else {
        Resize-WithWpf -Source $srcPath -Width $w -Height $h -Dest $outFile
    }
}

Write-Host "Done. Generated $($files.Count) assets into $destPath"