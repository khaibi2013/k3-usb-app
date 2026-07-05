const { execSync } = require('child_process');
// create a mock diskpart script
const script = `select volume E\nattributes volume set readonly`;
console.log(script);
