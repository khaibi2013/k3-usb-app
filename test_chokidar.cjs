const fs = require('fs');
const path = require('path');
const config = { autoEncryptFolder: 'baomat1', autoEncryptEnabled: true };
const getUsbRoot = () => path.join(__dirname, 'test_usb');
if (!fs.existsSync(getUsbRoot())) fs.mkdirSync(getUsbRoot());
const folderName = config.autoEncryptFolder || 'BaoMat';
const watchDir = path.join(getUsbRoot(), folderName);
try {
    fs.mkdirSync(watchDir, { recursive: true });
    console.log("Created:", watchDir);
} catch(e) {
    console.log("Error:", e);
}
