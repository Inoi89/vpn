const { app, BrowserWindow, ipcMain, dialog } = require('electron');
const path = require('path');
const fs = require('fs');
const { spawn } = require('child_process');
const wintun = require('./wintun');

const CONF_DIR = path.join(app.getAppPath(), 'temp');
const CONF_PATH = path.join(CONF_DIR, 'imported.conf');
const CLEANED_PATH = path.join(CONF_DIR, 'cleaned.conf');
const WG_GO_PATH = path.join(app.getAppPath(), 'wireguard-go.exe');

let wgProc = null;
let adapterHandle = null;

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


function parseConf(text) {
  const lines = text.split(/\r?\n/);
  let inInterface = false;
  let address = '';
  const dns = [];
  const cleaned = [];
  for (const rawLine of lines) {
    const line = rawLine.trim();
    if (!line) continue;
    if (line === '[Interface]') {
      inInterface = true;
      cleaned.push(line);
      continue;
    }
    if (line.startsWith('[')) {
      inInterface = false;
      cleaned.push(line);
      continue;
    }
    if (inInterface) {
      if (line.startsWith('Address')) {
        const val = line.split('=')[1].split(',')[0].trim();
        address = val.split('/')[0];
      } else if (line.startsWith('DNS')) {
        dns.push(...line.split('=')[1].split(',').map((s) => s.trim()));
      } else if (line.startsWith('PrivateKey')) {
        cleaned.push(line);
      }
    } else {
      cleaned.push(line);
    }
  }
  return { cleanedConf: cleaned.join('\n') + '\n', address, dns };
}

ipcMain.handle('start-vpn', async () => {
  if (!fs.existsSync(WG_GO_PATH)) {
    win.webContents.send('log', 'wireguard-go.exe not found');
    return false;
  }
  if (!fs.existsSync(CONF_PATH)) {
    win.webContents.send('log', 'Config not imported');
    return false;
  }

  const confText = fs.readFileSync(CONF_PATH, 'utf8');
  const { cleanedConf } = parseConf(confText);
  fs.writeFileSync(CLEANED_PATH, cleanedConf);

  adapterHandle = wintun.createAdapter('SimVPN');

  wgProc = spawn(WG_GO_PATH, ['SimVPN']);
  wgProc.stdout.on('data', (d) => {
    const msg = d.toString();
    console.log(msg);
    win.webContents.send('log', msg);
  });
  wgProc.stderr.on('data', (d) => {
    const msg = d.toString();
    console.log(msg);
    win.webContents.send('log', msg);
  });

  wgProc.stdin.write(cleanedConf);
  wgProc.stdin.end();

  return true;
});

ipcMain.handle('stop-vpn', async () => {
  if (wgProc) {
    wgProc.kill();
    wgProc = null;
  }
  if (adapterHandle) {
    wintun.closeAdapter(adapterHandle);
    wintun.deleteDriver();
    adapterHandle = null;
  }
  return true;
});
