const si = require('systeminformation');
const root = "/Users/khaibi/Documents/Nguyễn Quang Khải/code/USB_mat/k3-usb-app";
si.fsSize().then(drives => {
    let bestDrive = null;
    let maxLen = -1;
    for (const d of drives) {
        if (root.startsWith(d.mount) && d.mount.length > maxLen) {
            maxLen = d.mount.length;
            bestDrive = d;
        }
    }
    console.log("Root:", root);
    console.log("BestDrive:", bestDrive);
});
