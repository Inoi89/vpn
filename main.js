const { app, BrowserWindow, ipcMain, dialog } = require('electron');
const path = require('path');
const fs = require('fs');
const { spawn } = require('child_process');

const CONF_DIR = path.join(app.getAppPath(), 'temp');
const CONF_PATH = path.join(CONF_DIR, 'imported.conf');
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
  if (!fs.existsSync(CONF_DIR)) {
    fs.mkdirSync(CONF_DIR, { recursive: true });
  }
  fs.copyFileSync(filePaths[0], CONF_PATH);
  return true;
});

function run(cmd, args) {
  return new Promise((resolve) => {
    const proc = spawn(cmd, args);
    proc.stdout.on('data', (d) => {
      const msg = d.toString();
      console.log(msg);
      win.webContents.send('log', msg);
    });
    proc.stderr.on('data', (d) => {
      const msg = d.toString();
      console.log(msg);
      win.webContents.send('log', msg);
    });
    proc.on('close', (code) => resolve(code === 0));
  });
}

ipcMain.handle('start-vpn', async () => {
  if (!fs.existsSync(WG_PATH)) {
    win.webContents.send('log', 'wg.exe not found');
    return false;
  }
  if (!fs.existsSync(CONF_PATH)) {
    win.webContents.send('log', 'Config not imported');
    return false;
  }

  let ok = await run(WG_PATH, ['setconf', 'SimVPN', CONF_PATH]);
  if (!ok) {
    ok = await run(WG_PATH, ['addconf', 'SimVPN', CONF_PATH]);
  }
  if (ok) {
    ok = await run('netsh', ['interface', 'set', 'interface', 'SimVPN', 'admin=enabled']);
  }
  return ok;
});

ipcMain.handle('stop-vpn', async () => {
  return run('netsh', ['interface', 'set', 'interface', 'SimVPN', 'admin=disabled']);
});
