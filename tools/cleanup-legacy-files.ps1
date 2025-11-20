param(
    [string]$RepoRoot = (Split-Path -Parent $MyInvocation.MyCommand.Path)
)

$paths = @(
    "ResizeMe/Helpers/PresetButtonBuilder.cs",
    "ResizeMe/Helpers/PresetPresenter.cs",
    "ResizeMe/Helpers/StatusManager.cs",
    "ResizeMe/Helpers/WindowAnimations.cs",
    "ResizeMe/Helpers/WindowPositionHelper.cs",
    "ResizeMe/Helpers/CompositionExtensions.cs",
    "ResizeMe/Services/TrayIconManager.cs",
    "ResizeMe/Services/WindowManager.cs",
    "ResizeMe/Services/WindowResizer.cs",
    "ResizeMe/Services/HotKeyManager.cs",
    "ResizeMe/Services/PresetManager.cs",
    "ResizeMe/Services/UserPreferences.cs"
)

foreach ($relative in $paths) {
    $fullPath = Join-Path $RepoRoot $relative
    if (Test-Path $fullPath) {
        Remove-Item $fullPath -Force
        Write-Host "Removed $relative"
    }
    else {
        Write-Host "Skipped $relative (not found)"
    }
}
