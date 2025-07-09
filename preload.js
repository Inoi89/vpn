const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('vpn', {
  importConfig: () => ipcRenderer.invoke('import-config'),
  startVpn: () => ipcRenderer.invoke('start-vpn'),
  stopVpn: () => ipcRenderer.invoke('stop-vpn'),
  onLog: (cb) => ipcRenderer.on('log', (_e, msg) => cb(msg)),
});
