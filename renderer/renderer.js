let connected = false;

function setStatus(text, cls) {
  const el = document.getElementById('status');
  el.className = cls;
  el.textContent = text;
}

document.getElementById('import').addEventListener('click', async () => {
  const ok = await window.vpn.importConfig();
  if (ok) setStatus('Config imported', 'disconnected');
});

document.getElementById('fetch').addEventListener('click', async () => {
  const token = document.getElementById('token').value.trim();
  if (!token) return alert('Token required');
  const ok = await window.vpn.fetchConfig(token);
  setStatus(ok ? 'Config fetched' : 'Error', ok ? 'disconnected' : 'error');
});

document.getElementById('toggle').addEventListener('click', async () => {
  if (!connected) {
    const ok = await window.vpn.connect();
    setStatus(ok ? 'Connected' : 'Error', ok ? 'connected' : 'error');
    if (ok) {
      connected = true;
      document.getElementById('toggle').textContent = 'Disconnect';
    }
  } else {
    const ok = await window.vpn.disconnect();
    setStatus(ok ? 'Disconnected' : 'Error', ok ? 'disconnected' : 'error');
    if (ok) {
      connected = false;
      document.getElementById('toggle').textContent = 'GO VPN';
    }
  }
});

window.vpn.onWgError((msg) => setStatus(msg, 'error'));
