const si = require('systeminformation');
si.blockDevices().then(data => {
  console.log(JSON.stringify(data, null, 2));
});
