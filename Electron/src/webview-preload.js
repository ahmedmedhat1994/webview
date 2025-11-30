const { ipcRenderer, contextBridge } = require('electron');

// Expose flutter_inappwebview - Send HTML directly to main process for printing
contextBridge.exposeInMainWorld('flutter_inappwebview', {
  callHandler: async (name, data) => {
    console.log('flutter_inappwebview.callHandler called:', name);

    if (name === 'printHandler') {
      if (data && data.toString().trim().length > 0) {
        const html = data.toString();
        console.log('Print HTML received, length:', html.length);

        // Send HTML to main process for proper thermal printing
        ipcRenderer.invoke('print-receipt', html);
      } else {
        // No HTML provided, print current page
        console.log('No HTML, printing current page');
        ipcRenderer.invoke('print-current-page');
      }
    }
    return Promise.resolve();
  }
});

// Listen for messages from web page (FAB buttons)
window.addEventListener('message', (event) => {
  if (!event.data || !event.data.type) return;

  switch (event.data.type) {
    case 'electron-go-home':
      ipcRenderer.invoke('go-home');
      break;

    case 'electron-open-settings':
      ipcRenderer.invoke('open-settings');
      break;
  }
});

// Log when preload is ready
console.log('Electron WebView preload script loaded - flutter_inappwebview with CSS');
