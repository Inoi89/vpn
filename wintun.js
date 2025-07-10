const path = require('path');
const ffi = require('ffi-napi');
const ref = require('ref-napi');

const dll = ffi.Library(path.join(__dirname, 'wintun'), {
  'WintunCreateAdapter': ['pointer', ['string', 'string', 'pointer']],
  'WintunOpenAdapter': ['pointer', ['string']],
  'WintunCloseAdapter': ['void', ['pointer']],
  'WintunDeleteDriver': ['bool', []],
});

function createAdapter(name) {
  let adapter = dll.WintunOpenAdapter(name);
  if (ref.isNull(adapter)) {
    adapter = dll.WintunCreateAdapter(name, 'WireGuard', ref.NULL);
  }
  return adapter;
}

function closeAdapter(adapter) {
  if (adapter && !ref.isNull(adapter)) dll.WintunCloseAdapter(adapter);
}

module.exports = { createAdapter, closeAdapter, deleteDriver: dll.WintunDeleteDriver };
