@echo off
title Installing ResizeMe...
echo ==========================================
echo      ResizeMe Installer
echo ==========================================
echo.

:: Check for Administrator privileges
net session >nul 2>&1
if %errorLevel% == 0 (
    goto :RunInstall
) else (
    echo [INFO] Requesting Administrator privileges to install to Program Files...
    goto :Elevate
)

:Elevate
    echo Set UAC = CreateObject^("Shell.Application"^) > "%temp%\getadmin.vbs"
    echo UAC.ShellExecute "cmd.exe", "/c ""%~s0"" %*", "", "runas", 1 >> "%temp%\getadmin.vbs"
    "%temp%\getadmin.vbs"
    del "%temp%\getadmin.vbs"
    exit /b

:RunInstall
cd /d "%~dp0"
set "TARGET_DIR=%ProgramFiles%\ResizeMe"
set "SOURCE_DIR=%~dp0data"

if not exist "%SOURCE_DIR%" (
    echo [ERROR] Could not find application data.
    echo Please ensure you extracted all files from the zip.
    pause
    exit /b 1
)

echo [INFO] Installing to %TARGET_DIR%...
if not exist "%TARGET_DIR%" mkdir "%TARGET_DIR%"

echo [INFO] Copying files...
xcopy /E /I /Y /Q "%SOURCE_DIR%\*" "%TARGET_DIR%\" >nul

echo [INFO] Creating shortcuts (All Users)...
set "SCRIPT=%TEMP%\CreateShortcut.ps1"
echo $WshShell = New-Object -ComObject WScript.Shell > "%SCRIPT%"

:: Create Desktop Shortcut (Public Desktop)
echo $DesktopPath = [Environment]::GetFolderPath("CommonDesktopDirectory") >> "%SCRIPT%"
echo $Shortcut = $WshShell.CreateShortcut("$DesktopPath\ResizeMe.lnk") >> "%SCRIPT%"
echo $Shortcut.TargetPath = "%TARGET_DIR%\ResizeMe.exe" >> "%SCRIPT%"
echo $Shortcut.Save() >> "%SCRIPT%"

:: Create Start Menu Shortcut (Common Programs)
echo $ProgramsPath = [Environment]::GetFolderPath("CommonPrograms") >> "%SCRIPT%"
echo $Shortcut = $WshShell.CreateShortcut("$ProgramsPath\ResizeMe.lnk") >> "%SCRIPT%"
echo $Shortcut.TargetPath = "%TARGET_DIR%\ResizeMe.exe" >> "%SCRIPT%"
echo $Shortcut.Save() >> "%SCRIPT%"

powershell -ExecutionPolicy Bypass -File "%SCRIPT%"
del "%SCRIPT%"

echo.
echo [SUCCESS] Installation Complete!
echo.
echo You can now run ResizeMe from your Desktop or Start Menu.
echo You can delete this installer folder and the zip file.
echo.
pause