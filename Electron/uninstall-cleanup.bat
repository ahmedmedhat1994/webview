@echo off
echo ========================================
echo    Vopecs POS - Cleanup Script
echo ========================================
echo.

echo Closing Vopecs POS if running...
taskkill /F /IM "Vopecs POS.exe" 2>nul
timeout /t 2 >nul

echo.
echo Removing application data...

:: Remove app data from Local AppData
if exist "%LOCALAPPDATA%\vopecs-pos" (
    rmdir /S /Q "%LOCALAPPDATA%\vopecs-pos"
    echo [OK] Removed: %LOCALAPPDATA%\vopecs-pos
) else (
    echo [SKIP] Not found: %LOCALAPPDATA%\vopecs-pos
)

:: Remove app data from Roaming AppData
if exist "%APPDATA%\vopecs-pos" (
    rmdir /S /Q "%APPDATA%\vopecs-pos"
    echo [OK] Removed: %APPDATA%\vopecs-pos
) else (
    echo [SKIP] Not found: %APPDATA%\vopecs-pos
)

:: Remove app data with product name
if exist "%LOCALAPPDATA%\Vopecs POS" (
    rmdir /S /Q "%LOCALAPPDATA%\Vopecs POS"
    echo [OK] Removed: %LOCALAPPDATA%\Vopecs POS
) else (
    echo [SKIP] Not found: %LOCALAPPDATA%\Vopecs POS
)

if exist "%APPDATA%\Vopecs POS" (
    rmdir /S /Q "%APPDATA%\Vopecs POS"
    echo [OK] Removed: %APPDATA%\Vopecs POS
) else (
    echo [SKIP] Not found: %APPDATA%\Vopecs POS
)

:: Remove Electron cache
if exist "%APPDATA%\Electron" (
    rmdir /S /Q "%APPDATA%\Electron"
    echo [OK] Removed: %APPDATA%\Electron
) else (
    echo [SKIP] Not found: %APPDATA%\Electron
)

:: Remove from Program Files if installed
if exist "%PROGRAMFILES%\Vopecs POS" (
    rmdir /S /Q "%PROGRAMFILES%\Vopecs POS"
    echo [OK] Removed: %PROGRAMFILES%\Vopecs POS
) else (
    echo [SKIP] Not found: %PROGRAMFILES%\Vopecs POS
)

if exist "%PROGRAMFILES(x86)%\Vopecs POS" (
    rmdir /S /Q "%PROGRAMFILES(x86)%\Vopecs POS"
    echo [OK] Removed: %PROGRAMFILES(x86)%\Vopecs POS
) else (
    echo [SKIP] Not found: %PROGRAMFILES(x86)%\Vopecs POS
)

:: Remove Desktop shortcut
if exist "%USERPROFILE%\Desktop\Vopecs POS.lnk" (
    del /F "%USERPROFILE%\Desktop\Vopecs POS.lnk"
    echo [OK] Removed: Desktop shortcut
) else (
    echo [SKIP] Not found: Desktop shortcut
)

:: Remove Start Menu shortcut
if exist "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Vopecs POS.lnk" (
    del /F "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Vopecs POS.lnk"
    echo [OK] Removed: Start Menu shortcut
) else (
    echo [SKIP] Not found: Start Menu shortcut
)

echo.
echo ========================================
echo    Cleanup Complete!
echo ========================================
echo.
pause
