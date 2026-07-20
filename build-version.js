const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');
const h = execSync('git rev-parse --short HEAD').toString().trim();
const outDir = path.join(__dirname, 'frontBuild', '_app');
fs.mkdirSync(outDir, { recursive: true });
fs.writeFileSync(path.join(outDir, 'version.json'), JSON.stringify({ version: h }));
console.log('version.json ->', h);
