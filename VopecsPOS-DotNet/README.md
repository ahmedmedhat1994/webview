# Vopecs POS - Windows (.NET)

A Windows desktop application for Vopecs POS using .NET 6 and WebView2.

## Requirements

- Windows 10/11
- .NET 6.0 Runtime (included in self-contained build)
- WebView2 Runtime (usually pre-installed on Windows 10/11)

## Features

- Modern splash screen
- URL configuration and storage
- WebView2 for displaying web content
- Native Windows printing support
- Floating action button for navigation
- Settings persistence

## Building

### Using Visual Studio
1. Open `VopecsPOS.csproj` in Visual Studio 2022
2. Build > Publish

### Using Command Line
```bash
cd VopecsPOS-DotNet
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

### Using Build Script
Double-click `build.bat`

## Output

The built executable will be in `./publish/VopecsPOS.exe`

## Installing WebView2 Runtime

If WebView2 is not installed, download from:
https://developer.microsoft.com/en-us/microsoft-edge/webview2/

Download "Evergreen Bootstrapper" and run it.
