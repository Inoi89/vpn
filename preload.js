const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('vpn', {
  importConfig: () => ipcRenderer.invoke('import-config'),
  fetchConfig: (token) => ipcRenderer.invoke('fetch-config', token),
  connect: () => ipcRenderer.invoke('connect'),
  disconnect: () => ipcRenderer.invoke('disconnect'),
  onWgError: (cb) => ipcRenderer.on('wg-error', (_e, msg) => cb(msg)),
});
