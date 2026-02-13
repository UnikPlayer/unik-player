const { exec } = require('child_process');

const DEFAULT_SITE_URL = 'http://localhost:27272';

function openSite(url = DEFAULT_SITE_URL) { // <- дефолт, если не передан
  if (!url) {
    console.error('URL не задан!');
    return;
  }

  const cmd = process.platform === 'win32'
    ? `start "" "${url}"`
    : process.platform === 'darwin'
      ? `open "${url}"`
      : `xdg-open "${url}"`;

  exec(cmd, err => {
    if (err) console.error('Не смог открыть сайт:', err);
  });
}

module.exports = { openSite };
