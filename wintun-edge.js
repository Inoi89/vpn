const edge = require('edge-js');
const path = require('path');

const wintunDll = path.join(__dirname, 'WintunWrapper.dll');

const createAdapterFunc = edge.func({
  assemblyFile: wintunDll,
  typeName: 'WintunWrapper.AdapterManager',
  methodName: 'CreateAdapter'
});

const deleteAdapterFunc = edge.func({
  assemblyFile: wintunDll,
  typeName: 'WintunWrapper.AdapterManager',
  methodName: 'DeleteAdapter'
});

module.exports = {
  createAdapter: () => new Promise((resolve, reject) => {
    createAdapterFunc(null, (err, result) => err ? reject(err) : resolve(result));
  }),
  deleteAdapter: () => new Promise((resolve, reject) => {
    deleteAdapterFunc(null, (err, result) => err ? reject(err) : resolve(result));
  })
};
