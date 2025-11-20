@echo off
REM ResizeMe Build & Release Script (Windows batch)
REM Builds "Simple Installer" zip packages for distribution

setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
set PROJECT_ROOT=%SCRIPT_DIR%..
set PROJECT_FILE=%PROJECT_ROOT%\ResizeMe\ResizeMe.csproj
set RELEASE_DIR=%PROJECT_ROOT%\releases
set DIST_DIR=%PROJECT_ROOT%\ResizeMe_Dist

echo.
echo === ResizeMe Build Release Script ===
echo Project: %PROJECT_FILE%
echo Release dir: %RELEASE_DIR%
echo.

REM Clean old releases
if exist "%RELEASE_DIR%" rmdir /s /q "%RELEASE_DIR%"
mkdir "%RELEASE_DIR%"

REM Define platforms
set PLATFORMS=x64 x86 arm64

for %%P in (%PLATFORMS%) do (
    echo.
    echo Building %%P...
    
    REM Clean dist dir
    if exist "%DIST_DIR%" rmdir /s /q "%DIST_DIR%"
    mkdir "%DIST_DIR%\data"

    REM Publish
    dotnet publish "%PROJECT_FILE%" ^
      -c Release ^
      -p:SelfContained=true ^
      -p:PublishSingleFile=false ^
      -p:PublishTrimmed=false ^
      -r win-%%P ^
      -p:WindowsPackageType=None

    REM Copy files to data folder
    xcopy /E /I /Y /Q "%PROJECT_ROOT%\ResizeMe\bin\Release\net8.0-windows10.0.19041.0\win-%%P\publish\*" "%DIST_DIR%\data\" >nul
    
    REM Copy installer script
    copy "%SCRIPT_DIR%\Install ResizeMe.bat" "%DIST_DIR%\" >nul

    REM Zip it up
    echo Creating zip package...
    powershell -Command "Compress-Archive -Path '%DIST_DIR%\*' -DestinationPath '%RELEASE_DIR%\ResizeMe-%%P-Installer.zip' -Force"
    
    echo [OK] Created ResizeMe-%%P-Installer.zip
)

REM Cleanup
if exist "%DIST_DIR%" rmdir /s /q "%DIST_DIR%"

echo.
echo === Build complete! ===
echo Releases in: %RELEASE_DIR%
dir /b "%RELEASE_DIR%"

endlocal
