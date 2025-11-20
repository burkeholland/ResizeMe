@echo off
REM ResizeMe Build & Release Script (Windows batch)
REM Builds self-contained .exe files for distribution

setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
set PROJECT_ROOT=%SCRIPT_DIR%..
set PROJECT_FILE=%PROJECT_ROOT%\ResizeMe\ResizeMe.csproj
set RELEASE_DIR=%PROJECT_ROOT%\releases

echo.
echo === ResizeMe Build Release Script ===
echo Project: %PROJECT_FILE%
echo Release dir: %RELEASE_DIR%
echo.

REM Clean old releases
if exist "%RELEASE_DIR%" rmdir /s /q "%RELEASE_DIR%"
mkdir "%RELEASE_DIR%"

REM Build x64 standalone
echo Building x64 standalone...
dotnet publish "%PROJECT_FILE%" ^
  -c Release ^
  -p:PublishProfile="win-x64-standalone" ^
  -p:Platform="x64" ^
  -p:IsStandaloneRelease=true

set X64_EXE=%PROJECT_ROOT%\ResizeMe\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\ResizeMe.exe
if exist "%X64_EXE%" (
  copy "%X64_EXE%" "%RELEASE_DIR%\ResizeMe-x64.exe" >nul
  echo [OK] Created ResizeMe-x64.exe
) else (
  echo [WARN] Could not find x64 exe at %X64_EXE%
)

REM Build x86 standalone
echo.
echo Building x86 standalone...
dotnet publish "%PROJECT_FILE%" ^
  -c Release ^
  -p:PublishProfile="win-x86-standalone" ^
  -p:Platform="x86" ^
  -p:IsStandaloneRelease=true

set X86_EXE=%PROJECT_ROOT%\ResizeMe\bin\Release\net8.0-windows10.0.19041.0\win-x86\publish\ResizeMe.exe
if exist "%X86_EXE%" (
  copy "%X86_EXE%" "%RELEASE_DIR%\ResizeMe-x86.exe" >nul
  echo [OK] Created ResizeMe-x86.exe
) else (
  echo [WARN] Could not find x86 exe at %X86_EXE%
)

echo.
echo === Build complete! ===
echo Releases in: %RELEASE_DIR%
dir /s "%RELEASE_DIR%"

endlocal
