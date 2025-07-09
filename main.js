const { app, BrowserWindow, ipcMain, dialog } = require('electron');
const path = require('path');
const fs = require('fs');
const axios = require('axios');
const { spawn } = require('child_process');

const API_KEY = 'YOUR_API_KEY_HERE';
const CONF_PATH = path.join(app.getPath('temp'), 'simvpn.conf');
const WG_PATH = path.join(app.getAppPath(), 'wg.exe');

let win;

function createWindow() {
  win = new BrowserWindow({
    width: 400,
    height: 300,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
    },
  });

  win.loadFile(path.join(__dirname, 'renderer', 'index.html'));
}

app.whenReady().then(createWindow);

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});

ipcMain.handle('import-config', async () => {
  const { canceled, filePaths } = await dialog.showOpenDialog(win, {
    filters: [{ name: 'Config', extensions: ['conf'] }],
    properties: ['openFile'],
  });
  if (canceled || !filePaths[0]) return false;
  fs.copyFileSync(filePaths[0], CONF_PATH);
  return true;
});

ipcMain.handle('fetch-config', async (_event, token) => {
  try {
    const { data } = await axios.post(
      'https://vpn.my-gateway.com/api/getConf',
      { userToken: token },
      { headers: { 'X-Api-Key': API_KEY } }
    );
    fs.writeFileSync(CONF_PATH, data.wgConf);
    return true;
  } catch (e) {
    console.error(e);
    return false;
  }
});

function runWg(args, onClose) {
  if (!fs.existsSync(WG_PATH)) {
    win.webContents.send('wg-error', 'wg.exe not found');
    return;
  }
  const proc = spawn(WG_PATH, args);
  proc.stdout.on('data', (d) => console.log(d.toString()));
  proc.stderr.on('data', (d) => console.log(d.toString()));
  proc.on('close', (code) => onClose(code));
}

ipcMain.handle('connect', () => {
  return new Promise((resolve) => {
    runWg(['quick', 'up', CONF_PATH], (code) => resolve(code === 0));
  });
});

ipcMain.handle('disconnect', () => {
  return new Promise((resolve) => {
    runWg(['quick', 'down', CONF_PATH], (code) => resolve(code === 0));
  });
});
