@echo off
echo ========================================
echo    Vopecs POS - Cache Cleanup Script
echo ========================================
echo.

echo Closing Vopecs POS if running...
taskkill /F /IM "vopecs_pos.exe" 2>nul
timeout /t 2 >nul

echo.
echo Removing application cache...

:: Remove Flutter Windows app data
if exist "%LOCALAPPDATA%\vopecs_pos" (
    rmdir /S /Q "%LOCALAPPDATA%\vopecs_pos"
    echo [OK] Removed: %LOCALAPPDATA%\vopecs_pos
) else (
    echo [SKIP] Not found: %LOCALAPPDATA%\vopecs_pos
)

:: Remove with different naming
if exist "%LOCALAPPDATA%\Vopecs POS" (
    rmdir /S /Q "%LOCALAPPDATA%\Vopecs POS"
    echo [OK] Removed: %LOCALAPPDATA%\Vopecs POS
) else (
    echo [SKIP] Not found: %LOCALAPPDATA%\Vopecs POS
)

:: Remove from Roaming AppData
if exist "%APPDATA%\vopecs_pos" (
    rmdir /S /Q "%APPDATA%\vopecs_pos"
    echo [OK] Removed: %APPDATA%\vopecs_pos
) else (
    echo [SKIP] Not found: %APPDATA%\vopecs_pos
)

if exist "%APPDATA%\Vopecs POS" (
    rmdir /S /Q "%APPDATA%\Vopecs POS"
    echo [OK] Removed: %APPDATA%\Vopecs POS
) else (
    echo [SKIP] Not found: %APPDATA%\Vopecs POS
)

:: Remove WebView2 cache
if exist "%LOCALAPPDATA%\vopecs_pos\EBWebView" (
    rmdir /S /Q "%LOCALAPPDATA%\vopecs_pos\EBWebView"
    echo [OK] Removed WebView2 cache
)

:: Remove shared_preferences
if exist "%APPDATA%\vopecs_pos\shared_preferences.json" (
    del /F "%APPDATA%\vopecs_pos\shared_preferences.json"
    echo [OK] Removed shared_preferences
)

:: Remove any temp files
if exist "%TEMP%\vopecs*" (
    del /F /Q "%TEMP%\vopecs*" 2>nul
    echo [OK] Removed temp files
)

echo.
echo ========================================
echo    Cache Cleanup Complete!
echo ========================================
echo.
echo You can now restart Vopecs POS.
echo.
pause
