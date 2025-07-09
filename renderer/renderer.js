function setStatus(text, cls) {
  const el = document.getElementById('status');
  el.className = cls;
  el.textContent = text;
}

document.getElementById('import').addEventListener('click', async () => {
  const ok = await window.vpn.importConfig();
  if (ok) setStatus('Config imported', 'disconnected');
});

document.getElementById('start').addEventListener('click', async () => {
  const ok = await window.vpn.startVpn();
  setStatus(ok ? 'Connected' : 'Error', ok ? 'connected' : 'error');
});

document.getElementById('stop').addEventListener('click', async () => {
  const ok = await window.vpn.stopVpn();
  setStatus(ok ? 'Disconnected' : 'Error', ok ? 'disconnected' : 'error');
});

window.vpn.onLog((msg) => console.log(msg));
