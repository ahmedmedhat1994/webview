@echo off
echo ========================================
echo    Building Vopecs POS (.NET)
echo ========================================
echo.

:: Restore packages
echo Restoring packages...
dotnet restore

:: Build release
echo.
echo Building release...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish

echo.
echo ========================================
echo    Build Complete!
echo ========================================
echo.
echo Output: ./publish/VopecsPOS.exe
echo.
pause
