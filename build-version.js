const fs = require('fs');
const { execSync } = require('child_process');
const h = execSync('git rev-parse --short HEAD').toString().trim();
fs.writeFileSync('C:/Users/000-d/Desktop/JShit/Unik player reps/unikPlayer/frontBuild/_app/version.json', JSON.stringify({ version: h }));
console.log('version.json ->', h);
