const { contextBridge, ipcRenderer } = require('electron');

// Expose protected methods to renderer process
contextBridge.exposeInMainWorld('electronAPI', {
  // Settings
  getSettings: () => ipcRenderer.invoke('get-settings'),
  saveSettings: (settings) => ipcRenderer.invoke('save-settings', settings),
  loadWebView: () => ipcRenderer.invoke('load-webview'),

  // Navigation
  goHome: () => ipcRenderer.invoke('go-home'),
  openSettings: () => ipcRenderer.invoke('open-settings'),

  // Printing
  print: (html) => ipcRenderer.invoke('print', html),
  silentPrint: (html) => ipcRenderer.invoke('silent-print', html),
  getPrinters: () => ipcRenderer.invoke('get-printers'),
  testPrint: () => ipcRenderer.invoke('test-print'),
  printPreview: (html) => ipcRenderer.invoke('print-preview', html),
});
