const chokidar = require('chokidar');
const path = require('path');
const fs = require('fs');

const watchDir = path.join(__dirname, 'BaoMat');
fs.mkdirSync(watchDir, { recursive: true });

const vaultWatcher = chokidar.watch(watchDir, {
    persistent: true,
    ignoreInitial: true,
    awaitWriteFinish: {
        stabilityThreshold: 1000,
        pollInterval: 100
    }
});

vaultWatcher.on('add', (filePath) => {
    console.log('Detected add:', filePath);
});

console.log('Watching:', watchDir);

setTimeout(() => {
    fs.writeFileSync(path.join(watchDir, 'test_added.txt'), 'test data');
    console.log('Created test_added.txt');
}, 2000);

setTimeout(() => {
    process.exit(0);
}, 5000);
