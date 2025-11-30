const { app, BrowserWindow, BrowserView, ipcMain, Menu, dialog } = require('electron');
const path = require('path');
const Store = require('electron-store');
const fs = require('fs');
const os = require('os');

// Initialize store for settings
const store = new Store({
  defaults: {
    url: '',
    lastVisitedUrl: '',
    language: 'en',
    windowBounds: { width: 1280, height: 800 },
    // Printer settings
    printerName: '',
    printerType: 'thermal80', // standard, thermal58, thermal80
    silentPrint: false,
    // Print layout settings
    printMargins: { top: 0, right: 0, bottom: 0, left: 0 },
    printFontSize: 12
  }
});

let mainWindow;
let webView;
let settingsWindow;

function createWindow() {
  const { width, height } = store.get('windowBounds');

  mainWindow = new BrowserWindow({
    width: width,
    height: height,
    minWidth: 800,
    minHeight: 600,
    title: 'Vopecs POS',
    icon: path.join(__dirname, 'assets', 'icon.png'),
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true,
      preload: path.join(__dirname, 'preload.js')
    }
  });

  // Save window size on resize
  mainWindow.on('resize', () => {
    const { width, height } = mainWindow.getBounds();
    store.set('windowBounds', { width, height });
  });

  // Check if URL is set
  const savedUrl = store.get('url');
  if (savedUrl) {
    loadWebView(savedUrl);
  } else {
    loadSettingsPage();
  }

  // Create menu
  createMenu();

  mainWindow.on('closed', () => {
    mainWindow = null;
  });
}

function loadWebView(url) {
  // Remove existing webview if any
  if (webView) {
    mainWindow.removeBrowserView(webView);
    webView = null;
  }

  webView = new BrowserView({
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true,
      preload: path.join(__dirname, 'webview-preload.js')
    }
  });

  mainWindow.setBrowserView(webView);

  const bounds = mainWindow.getBounds();
  webView.setBounds({ x: 0, y: 0, width: bounds.width, height: bounds.height });
  webView.setAutoResize({ width: true, height: true });

  // Load the URL
  const urlToLoad = store.get('lastVisitedUrl') || url;
  webView.webContents.loadURL(urlToLoad);

  // Save current URL on navigation
  webView.webContents.on('did-navigate', (event, url) => {
    store.set('lastVisitedUrl', url);
  });

  webView.webContents.on('did-navigate-in-page', (event, url) => {
    store.set('lastVisitedUrl', url);
  });

  // Handle new window requests
  webView.webContents.setWindowOpenHandler(({ url }) => {
    webView.webContents.loadURL(url);
    return { action: 'deny' };
  });

  // Handle print requests from web page
  webView.webContents.on('did-finish-load', () => {
    const lang = store.get('language') || 'en';
    const texts = {
      en: { home: 'Home', settings: 'Settings', menu: 'Menu' },
      ar: { home: 'الرئيسية', settings: 'الإعدادات', menu: 'القائمة' }
    };
    const t = texts[lang] || texts.en;

    // Inject floating buttons only (flutter_inappwebview is exposed via preload)
    webView.webContents.executeJavaScript(`
      (function() {
        // Log that we're in Electron
        console.log('Electron: flutter_inappwebview available:', typeof window.flutter_inappwebview !== 'undefined');

        // Remove existing FAB if any
        var existingFab = document.getElementById('electron-fab-container');
        if (existingFab) existingFab.remove();

        // Create floating action button
        var fabContainer = document.createElement('div');
        fabContainer.id = 'electron-fab-container';
        fabContainer.innerHTML = \`
          <style>
            #electron-fab-container {
              position: fixed;
              bottom: 20px;
              right: 20px;
              z-index: 999999;
              font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            }
            #electron-fab-menu {
              display: none;
              flex-direction: column;
              align-items: flex-end;
              margin-bottom: 12px;
              gap: 8px;
            }
            #electron-fab-menu.show {
              display: flex;
            }
            .electron-fab-item {
              display: flex;
              align-items: center;
              gap: 8px;
              padding: 8px 12px;
              background: white;
              border-radius: 24px;
              box-shadow: 0 2px 8px rgba(0,0,0,0.15);
              cursor: pointer;
              transition: all 0.2s;
              border: none;
              font-size: 14px;
              font-weight: 500;
              color: #333;
            }
            .electron-fab-item:hover {
              transform: scale(1.05);
              box-shadow: 0 4px 12px rgba(0,0,0,0.2);
            }
            .electron-fab-item svg {
              width: 18px;
              height: 18px;
            }
            .electron-fab-icon {
              width: 32px;
              height: 32px;
              background: #2196F3;
              border-radius: 50%;
              display: flex;
              align-items: center;
              justify-content: center;
            }
            .electron-fab-icon svg {
              width: 16px;
              height: 16px;
              color: white;
              fill: white;
            }
            #electron-fab-main {
              width: 48px;
              height: 48px;
              background: linear-gradient(135deg, #2196F3, #1976D2);
              border-radius: 50%;
              border: none;
              cursor: pointer;
              display: flex;
              align-items: center;
              justify-content: center;
              box-shadow: 0 4px 12px rgba(33, 150, 243, 0.4);
              transition: all 0.3s;
            }
            #electron-fab-main:hover {
              transform: scale(1.1);
              box-shadow: 0 6px 16px rgba(33, 150, 243, 0.5);
            }
            #electron-fab-main svg {
              width: 24px;
              height: 24px;
              color: white;
              fill: white;
              transition: transform 0.3s;
            }
            #electron-fab-main.open svg {
              transform: rotate(45deg);
            }
            @media print {
              #electron-fab-container { display: none !important; }
            }
          </style>
          <div id="electron-fab-menu">
            <button class="electron-fab-item" onclick="window.postMessage({type:'electron-go-home'},'*')">
              <span>${t.home}</span>
              <div class="electron-fab-icon">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"></path>
                  <polyline points="9 22 9 12 15 12 15 22"></polyline>
                </svg>
              </div>
            </button>
            <button class="electron-fab-item" onclick="window.postMessage({type:'electron-open-settings'},'*')">
              <span>${t.settings}</span>
              <div class="electron-fab-icon">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <circle cx="12" cy="12" r="3"></circle>
                  <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"></path>
                </svg>
              </div>
            </button>
          </div>
          <button id="electron-fab-main" title="${t.menu}">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <line x1="12" y1="5" x2="12" y2="19"></line>
              <line x1="5" y1="12" x2="19" y2="12"></line>
            </svg>
          </button>
        \`;
        document.body.appendChild(fabContainer);

        // Toggle menu
        document.getElementById('electron-fab-main').addEventListener('click', function() {
          var menu = document.getElementById('electron-fab-menu');
          var btn = document.getElementById('electron-fab-main');
          menu.classList.toggle('show');
          btn.classList.toggle('open');
        });

        // Close menu when clicking outside
        document.addEventListener('click', function(e) {
          if (!fabContainer.contains(e.target)) {
            document.getElementById('electron-fab-menu').classList.remove('show');
            document.getElementById('electron-fab-main').classList.remove('open');
          }
        });
      })();
    `);
  });

  // Listen for messages from injected buttons
  webView.webContents.on('console-message', (event, level, message) => {
    // Handle console messages if needed
  });
}

function loadSettingsPage() {
  mainWindow.loadFile(path.join(__dirname, 'settings.html'));
}

function createMenu() {
  const template = [
    {
      label: 'File',
      submenu: [
        {
          label: 'Settings',
          accelerator: 'CmdOrCtrl+,',
          click: () => openSettings()
        },
        {
          label: 'Home',
          accelerator: 'CmdOrCtrl+H',
          click: () => goHome()
        },
        { type: 'separator' },
        {
          label: 'Exit',
          accelerator: 'Alt+F4',
          click: () => app.quit()
        }
      ]
    },
    {
      label: 'View',
      submenu: [
        {
          label: 'Reload',
          accelerator: 'CmdOrCtrl+R',
          click: () => {
            if (webView) webView.webContents.reload();
          }
        },
        {
          label: 'Clear Cache & Reload',
          accelerator: 'CmdOrCtrl+Shift+R',
          click: async () => {
            if (webView) {
              await webView.webContents.session.clearCache();
              webView.webContents.reload();
            }
          }
        },
        { type: 'separator' },
        {
          label: 'Zoom In',
          accelerator: 'CmdOrCtrl+Plus',
          click: () => {
            if (webView) {
              const zoom = webView.webContents.getZoomFactor();
              webView.webContents.setZoomFactor(zoom + 0.1);
            }
          }
        },
        {
          label: 'Zoom Out',
          accelerator: 'CmdOrCtrl+-',
          click: () => {
            if (webView) {
              const zoom = webView.webContents.getZoomFactor();
              webView.webContents.setZoomFactor(Math.max(0.1, zoom - 0.1));
            }
          }
        },
        {
          label: 'Reset Zoom',
          accelerator: 'CmdOrCtrl+0',
          click: () => {
            if (webView) webView.webContents.setZoomFactor(1);
          }
        },
        { type: 'separator' },
        {
          label: 'Toggle Fullscreen',
          accelerator: 'F11',
          click: () => {
            mainWindow.setFullScreen(!mainWindow.isFullScreen());
          }
        },
        {
          label: 'Developer Tools',
          accelerator: 'F12',
          click: () => {
            if (webView) webView.webContents.toggleDevTools();
          }
        }
      ]
    },
    {
      label: 'Print',
      submenu: [
        {
          label: 'Print Page',
          accelerator: 'CmdOrCtrl+P',
          click: () => printPage()
        },
        {
          label: 'Silent Print',
          accelerator: 'CmdOrCtrl+Shift+P',
          click: () => silentPrint()
        }
      ]
    }
  ];

  const menu = Menu.buildFromTemplate(template);
  Menu.setApplicationMenu(menu);
}

function openSettings() {
  if (settingsWindow) {
    settingsWindow.focus();
    return;
  }

  settingsWindow = new BrowserWindow({
    width: 650,
    height: 750,
    parent: mainWindow,
    modal: true,
    resizable: true,
    minimizable: false,
    title: 'Settings',
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true,
      preload: path.join(__dirname, 'preload.js')
    }
  });

  settingsWindow.loadFile(path.join(__dirname, 'settings.html'));
  settingsWindow.setMenu(null);

  settingsWindow.on('closed', () => {
    settingsWindow = null;
  });
}

function goHome() {
  const savedUrl = store.get('url');
  if (savedUrl && webView) {
    webView.webContents.loadURL(savedUrl);
  }
}

async function printPage() {
  if (!webView) return;

  try {
    await webView.webContents.print({
      silent: false,
      printBackground: true
    });
  } catch (e) {
    console.error('Print error:', e);
  }
}

async function silentPrint() {
  if (!webView) return;

  try {
    const printers = await webView.webContents.getPrintersAsync();
    const savedPrinterName = store.get('printerName');
    const useSilent = store.get('silentPrint');

    // Find selected printer or default
    let printer = printers.find(p => p.name === savedPrinterName);
    if (!printer) {
      printer = printers.find(p => p.isDefault);
    }

    if (printer) {
      await webView.webContents.print({
        silent: useSilent,
        printBackground: true,
        deviceName: printer.name
      });
    } else {
      await printPage();
    }
  } catch (e) {
    console.error('Silent print error:', e);
    await printPage();
  }
}

// IPC Handlers
// IPC handler for go-home from FAB
ipcMain.handle('go-home', () => {
  goHome();
  return true;
});

// IPC handler for open-settings from FAB
ipcMain.handle('open-settings', () => {
  openSettings();
  return true;
});

ipcMain.handle('get-settings', () => {
  return {
    url: store.get('url'),
    language: store.get('language'),
    printerName: store.get('printerName'),
    printerType: store.get('printerType'),
    silentPrint: store.get('silentPrint'),
    printMargins: store.get('printMargins'),
    printFontSize: store.get('printFontSize')
  };
});

ipcMain.handle('save-settings', (event, settings) => {
  if (settings.url !== undefined) {
    // Normalize URL
    let url = settings.url.trim();
    if (url && !url.startsWith('http://') && !url.startsWith('https://')) {
      url = 'https://' + url;
    }
    store.set('url', url);
    store.set('lastVisitedUrl', url);
  }
  if (settings.language !== undefined) {
    store.set('language', settings.language);
  }
  if (settings.printerName !== undefined) {
    store.set('printerName', settings.printerName);
  }
  if (settings.printerType !== undefined) {
    store.set('printerType', settings.printerType);
  }
  if (settings.silentPrint !== undefined) {
    store.set('silentPrint', settings.silentPrint);
  }
  if (settings.printMargins !== undefined) {
    store.set('printMargins', settings.printMargins);
  }
  if (settings.printFontSize !== undefined) {
    store.set('printFontSize', settings.printFontSize);
  }
  return true;
});

ipcMain.handle('test-print', async () => {
  try {
    const printerName = store.get('printerName');
    const printerType = store.get('printerType') || 'standard';
    const useSilent = store.get('silentPrint') || false;

    // Paper width in mm
    let paperWidthMM = 210;
    if (printerType === 'thermal58') {
      paperWidthMM = 58;
    } else if (printerType === 'thermal80') {
      paperWidthMM = 80;
    }

    const testHtml = `<!DOCTYPE html>
<html dir="rtl">
<head>
<meta charset="UTF-8">
<title>Test Print</title>
<style>
* { margin: 0; padding: 0; box-sizing: border-box; }
@page { size: ${paperWidthMM}mm auto; margin: 0 !important; }
html, body {
  width: ${paperWidthMM}mm;
  font-family: Arial, sans-serif;
  font-size: 12px;
  margin: 0;
  padding: 2mm;
  text-align: center;
  direction: rtl;
}
h2 { margin: 8px 0; font-size: 14px; }
hr { border: none; border-top: 1px dashed #000; margin: 8px 0; }
p { margin: 4px 0; }
</style>
</head>
<body>
<h2>Test Print - طباعة تجريبية</h2>
<p>Vopecs POS</p>
<hr>
<p>Printer: ${printerName || 'Default'}</p>
<p>Type: ${printerType} (${paperWidthMM}mm)</p>
<p>Date: ${new Date().toLocaleString()}</p>
<hr>
<p><strong>Print Test OK</strong></p>
<p><strong>الطباعة تعمل بنجاح</strong></p>
</body>
</html>`;

    const windowWidth = printerType === 'thermal58' ? 250 : printerType === 'thermal80' ? 350 : 400;
    const printWindow = new BrowserWindow({
      show: false,
      width: windowWidth,
      height: 400,
      webPreferences: {
        nodeIntegration: false,
        contextIsolation: true
      }
    });

    const base64Html = Buffer.from(testHtml).toString('base64');

    await new Promise((resolve) => {
      printWindow.webContents.once('did-finish-load', resolve);
      printWindow.loadURL('data:text/html;base64,' + base64Html);
    });

    printWindow.showInactive();
    await new Promise(resolve => setTimeout(resolve, 300));
    printWindow.hide();

    const printers = await printWindow.webContents.getPrintersAsync();
    let printer = printers.find(p => p.name === printerName);
    if (!printer) {
      printer = printers.find(p => p.isDefault);
    }

    const pageSizeConfig = printerType === 'thermal58'
      ? { width: 58000, height: 100000 }
      : printerType === 'thermal80'
        ? { width: 80000, height: 100000 }
        : 'A4';

    return new Promise((resolve) => {
      printWindow.webContents.print({
        silent: useSilent,
        printBackground: true,
        deviceName: printer ? printer.name : undefined,
        pageSize: pageSizeConfig,
        margins: { marginType: 'none' },
        scaleFactor: 100
      }, (success, failureReason) => {
        setTimeout(() => {
          if (!printWindow.isDestroyed()) {
            printWindow.close();
          }
        }, 500);
        if (!success) {
          console.error('Print failed:', failureReason);
        }
        resolve(success);
      });
    });
  } catch (e) {
    console.error('Test print error:', e);
    return false;
  }
});

// Print Preview handler
ipcMain.handle('print-preview', async (event, html) => {
  try {
    const printerType = store.get('printerType') || 'thermal80';

    let paperWidthMM = 210;
    if (printerType === 'thermal58') {
      paperWidthMM = 58;
    } else if (printerType === 'thermal80') {
      paperWidthMM = 80;
    }

    const htmlContent = html || '<p>No content</p>';

    // Preview shows original HTML with minimal wrapper
    const previewHtml = `<!DOCTYPE html>
<html dir="rtl">
<head>
<meta charset="UTF-8">
<title>Print Preview</title>
<style>
  html { background: #f0f0f0; padding: 20px; }
  .preview-header {
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    background: #333;
    color: white;
    padding: 10px 20px;
    display: flex;
    justify-content: space-between;
    align-items: center;
    z-index: 1000;
    font-family: Arial, sans-serif;
  }
  .preview-header button {
    padding: 8px 16px;
    border: none;
    border-radius: 4px;
    cursor: pointer;
    font-size: 14px;
    margin-left: 10px;
  }
  .btn-print { background: #4CAF50; color: white; }
  .btn-close { background: #f44336; color: white; }
  .preview-content {
    margin-top: 60px;
    width: ${paperWidthMM}mm;
    margin-left: auto;
    margin-right: auto;
    background: white;
    box-shadow: 0 2px 10px rgba(0,0,0,0.2);
    padding: 2mm;
    min-height: 200px;
  }
  @media print {
    .preview-header { display: none; }
    .preview-content { margin: 0; box-shadow: none; }
  }
</style>
</head>
<body>
<div class="preview-header">
  <span>Print Preview - ${printerType} (${paperWidthMM}mm)</span>
  <div>
    <button class="btn-print" onclick="window.print()">Print</button>
    <button class="btn-close" onclick="window.close()">Close</button>
  </div>
</div>
<div class="preview-content">${htmlContent}</div>
</body>
</html>`;

    const windowWidth = printerType === 'thermal58' ? 400 : printerType === 'thermal80' ? 500 : 600;
    const previewWindow = new BrowserWindow({
      width: windowWidth,
      height: 700,
      title: 'Print Preview',
      webPreferences: {
        nodeIntegration: false,
        contextIsolation: true
      }
    });

    const base64Html = Buffer.from(previewHtml).toString('base64');
    previewWindow.loadURL('data:text/html;base64,' + base64Html);
    previewWindow.setMenu(null);

    return true;
  } catch (e) {
    console.error('Print preview error:', e);
    return false;
  }
});

ipcMain.handle('load-webview', () => {
  const url = store.get('url');
  if (url) {
    if (settingsWindow) {
      settingsWindow.close();
    }
    loadWebView(url);
  }
  return true;
});

ipcMain.handle('print', async (event, html) => {
  if (!webView && !html) return false;

  try {
    if (html) {
      // Print specific HTML content
      const printWindow = new BrowserWindow({
        show: false,
        width: 800,
        height: 600,
        webPreferences: {
          nodeIntegration: false,
          contextIsolation: true
        }
      });

      const fullHtml = `<!DOCTYPE html>
<html>
<head>
<meta charset="UTF-8">
<style>
body { font-family: Arial, sans-serif; margin: 0; padding: 10px; }
@page { margin: 5mm; }
</style>
</head>
<body>${html}</body>
</html>`;

      const base64Html = Buffer.from(fullHtml).toString('base64');

      // Wait for content to fully load
      await new Promise((resolve) => {
        printWindow.webContents.once('did-finish-load', resolve);
        printWindow.loadURL('data:text/html;base64,' + base64Html);
      });

      // Show briefly to force render on Windows, then hide
      printWindow.showInactive();
      await new Promise(resolve => setTimeout(resolve, 300));
      printWindow.hide();

      // Use callback-based print
      return new Promise((resolve) => {
        printWindow.webContents.print({
          silent: false,
          printBackground: true
        }, (success, failureReason) => {
          setTimeout(() => {
            if (!printWindow.isDestroyed()) {
              printWindow.close();
            }
          }, 500);
          if (!success) {
            console.error('Print failed:', failureReason);
          }
          resolve(success);
        });
      });
    } else {
      await printPage();
      return true;
    }
  } catch (e) {
    console.error('Print error:', e);
    return false;
  }
});

// Print with HTML content - creates a properly sized window for thermal printers
ipcMain.handle('print-receipt', async (event, html) => {
  try {
    const printerName = store.get('printerName');
    const printerType = store.get('printerType') || 'thermal80';
    const useSilent = store.get('silentPrint') || false;

    console.log('Print receipt - Type:', printerType, 'Silent:', useSilent);

    // Paper dimensions in mm and pixels (assuming 96 DPI)
    let paperWidthMM = 80;
    let paperWidthPx = 302; // 80mm at 96 DPI
    if (printerType === 'thermal58') {
      paperWidthMM = 58;
      paperWidthPx = 219; // 58mm at 96 DPI
    } else if (printerType === 'standard') {
      paperWidthMM = 210;
      paperWidthPx = 794; // A4 width at 96 DPI
    }

    // Create full HTML document with proper styling for receipt
    const fullHtml = `<!DOCTYPE html>
<html dir="rtl">
<head>
  <meta charset="UTF-8">
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    @page {
      size: ${paperWidthMM}mm auto;
      margin: 0;
    }
    html, body {
      width: ${paperWidthMM}mm;
      font-family: Arial, sans-serif;
      font-size: 12px;
      line-height: 1.3;
      direction: rtl;
      text-align: right;
    }
    body {
      padding: 2mm;
    }
    /* Receipt styles from POS system */
    #invoice-POS, .invoice-POS { width: 100%; }
    .info { text-align: center; border-bottom: 1px dashed #000; padding-bottom: 5px; margin-bottom: 5px; }
    .invoice_logo img { width: 50px; height: 50px; object-fit: contain; }
    h2, h3 { font-size: 14px; margin: 3px 0; }
    p { margin: 2px 0; font-size: 11px; }
    table { width: 100%; border-collapse: collapse; }
    td { padding: 2px; font-size: 11px; }
    .tabletwo td { border-bottom: 1px dotted #ccc; }
    .change { margin-top: 5px; }
    .change th, .change td { border: 1px solid #ddd; padding: 2px; font-size: 10px; text-align: center; }
    .change thead { background: #f5f5f5; }
    #legalcopy { border-top: 1px dashed #000; padding-top: 5px; margin-top: 5px; text-align: center; }
    .legal { font-size: 11px; }
    .centred, .text-center { text-align: center; }
  </style>
</head>
<body>${html}</body>
</html>`;

    // Create print window with exact paper width
    const printWindow = new BrowserWindow({
      width: paperWidthPx,
      height: 800,
      show: false,
      webPreferences: {
        nodeIntegration: false,
        contextIsolation: true
      }
    });

    // Load HTML content
    await printWindow.loadURL('data:text/html;charset=utf-8,' + encodeURIComponent(fullHtml));

    // Wait for content to render
    await new Promise(resolve => setTimeout(resolve, 500));

    // Get printers
    const printers = await printWindow.webContents.getPrintersAsync();
    let printer = printers.find(p => p.name === printerName);
    if (!printer) {
      printer = printers.find(p => p.isDefault);
    }

    // Print with proper page size
    const pageSize = printerType === 'thermal58'
      ? { width: 58000, height: 200000 }
      : printerType === 'thermal80'
        ? { width: 80000, height: 200000 }
        : 'A4';

    return new Promise((resolve) => {
      printWindow.webContents.print({
        silent: useSilent,
        printBackground: true,
        deviceName: printer ? printer.name : undefined,
        pageSize: pageSize,
        margins: { marginType: 'none' },
        scaleFactor: 100
      }, (success, failureReason) => {
        setTimeout(() => {
          if (!printWindow.isDestroyed()) {
            printWindow.close();
          }
        }, 1000);
        if (!success) {
          console.error('Print failed:', failureReason);
        }
        resolve(success);
      });
    });
  } catch (e) {
    console.error('Print receipt error:', e);
    return false;
  }
});

// Print current page directly
ipcMain.handle('print-current-page', async () => {
  if (!webView) return false;

  try {
    const printerName = store.get('printerName');
    const printerType = store.get('printerType') || 'thermal80';
    const useSilent = store.get('silentPrint') || false;

    const printers = await webView.webContents.getPrintersAsync();
    let printer = printers.find(p => p.name === printerName);
    if (!printer) {
      printer = printers.find(p => p.isDefault);
    }

    const pageSize = printerType === 'thermal58'
      ? { width: 58000, height: 200000 }
      : printerType === 'thermal80'
        ? { width: 80000, height: 200000 }
        : 'A4';

    return new Promise((resolve) => {
      webView.webContents.print({
        silent: useSilent,
        printBackground: true,
        deviceName: printer ? printer.name : undefined,
        pageSize: pageSize,
        margins: { marginType: 'none' },
        scaleFactor: 100
      }, (success, failureReason) => {
        if (!success) {
          console.error('Print failed:', failureReason);
        }
        resolve(success);
      });
    });
  } catch (e) {
    console.error('Print error:', e);
    return false;
  }
});

ipcMain.handle('get-printers', async () => {
  try {
    // Create a temporary window to get printers if webView not available
    if (webView) {
      return await webView.webContents.getPrintersAsync();
    } else if (mainWindow) {
      return await mainWindow.webContents.getPrintersAsync();
    } else {
      const tempWindow = new BrowserWindow({ show: false });
      const printers = await tempWindow.webContents.getPrintersAsync();
      tempWindow.close();
      return printers;
    }
  } catch (e) {
    console.error('Get printers error:', e);
    return [];
  }
});

// App events
app.whenReady().then(createWindow);

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) {
    createWindow();
  }
});
