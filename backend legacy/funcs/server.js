// backend/funcs/server.js  (замени целиком)
const http = require('http');
const fs = require('fs');
const path = require('path');
const os = require('os');
const { fileURLToPath } = 'url';

//const __filename = fileURLToPath(import.meta.url);
//const __dirname = path.dirname(__filename);

const mimeMap = {
  html: 'text/html; charset=utf-8',
  js: 'text/javascript; charset=utf-8',
  mjs: 'text/javascript; charset=utf-8',
  css: 'text/css; charset=utf-8',
  json: 'application/json; charset=utf-8',
  png: 'image/png',
  jpg: 'image/jpeg',
  jpeg: 'image/jpeg',
  svg: 'image/svg+xml',
  ico: 'image/x-icon',
  webp: 'image/webp',
  woff: 'font/woff',
  woff2: 'font/woff2',
  ttf: 'font/ttf'
};

function ensureDirSync(p) {
  if (!fs.existsSync(p)) fs.mkdirSync(p, { recursive: true });
}

// Styles file path - saved in AppData/Local/UnikPlayer
const stylesDir = path.join(os.homedir(), 'AppData', 'Local', 'UnikPlayer');
const stylesFilePath = path.join(stylesDir, 'player-styles.json');
const cssDir = path.join(stylesDir, 'css');
const customPlayersDir = path.join(stylesDir, 'custom');

// HTML Validator
const { validateHTML, sanitizeHTML } = require('./htmlValidator');

// Media Filter
const { loadMediaFilter, saveMediaFilter, reloadFilter } = require('./mediaFilter');

function loadStyles() {
  try {
    if (fs.existsSync(stylesFilePath)) {
      const data = fs.readFileSync(stylesFilePath, 'utf-8');
      return JSON.parse(data);
    }
  } catch (e) {
    console.error('[Styles] Error loading styles:', e.message);
  }
  return {};
}

function saveStyles(styles) {
  try {
    ensureDirSync(stylesDir);
    fs.writeFileSync(stylesFilePath, JSON.stringify(styles, null, 2), 'utf-8');
    console.log('[Styles] Saved to', stylesFilePath);
    return true;
  } catch (e) {
    console.error('[Styles] Error saving styles:', e.message);
    return false;
  }
}

function copySnapshotRecursive(src, dest) {
  // Работает и для обычной FS, и для snapshot-псевдо-FS (когда файлы "встроены" в exe)
  try {
    const entries = fs.readdirSync(src, { withFileTypes: true });
    ensureDirSync(dest);
    for (const ent of entries) {
      const s = path.join(src, ent.name);
      const d = path.join(dest, ent.name);
      if (ent.isDirectory()) {
        copySnapshotRecursive(s, d);
      } else if (ent.isFile()) {
        const data = fs.readFileSync(s);
        fs.writeFileSync(d, data);
      }
    }
    return true;
  } catch (e) {
    return false;
  }
}

function startFrontendServer(options = {}) {
  const port = options.port || 27272;

  // возможные кандидаты на расположение frontBuild:
  const devCandidate = path.resolve(__dirname, '..', '..', 'frontBuild');             // dev (рядом с backend)
  const cwdCandidate = path.resolve(process.cwd(), 'frontBuild');                    // если запускаешь из корня
  const exeCandidate = path.join(path.dirname(process.execPath || process.argv[0]), 'frontBuild'); // рядом с exe

  // Определяем staticDir: приоритет
  let staticDir = options.staticDir ? path.resolve(options.staticDir) : null;

  // Если опция не указана — перебираем кандидатов
  if (!staticDir) {
    if (fs.existsSync(path.join(devCandidate, 'index.html'))) staticDir = devCandidate;
    else if (fs.existsSync(path.join(exeCandidate, 'index.html'))) staticDir = exeCandidate;
    else if (fs.existsSync(path.join(cwdCandidate, 'index.html'))) staticDir = cwdCandidate;
  }

  // Если всё ещё не найдено — пробуем распаковать из snapshot (встроенных ресурсов)
  if (!staticDir || !fs.existsSync(path.join(staticDir, 'index.html'))) {
    // src внутри snapshot часто может быть тот же относительный путь относительно __dirname
    const srcSnapshotPath = path.resolve(__dirname, '..', '..', 'frontBuild');
    const tmpDirBase = path.join(os.tmpdir(), 'unikplayer_frontBuild_' + Date.now());

    if (copySnapshotRecursive(srcSnapshotPath, tmpDirBase) && fs.existsSync(path.join(tmpDirBase, 'index.html'))) {
      staticDir = tmpDirBase;
      console.log('[FrontendServer] extracted frontBuild from snapshot ->', staticDir);
    } else {
      // если не распаковалось — пробуем ещё раз кандидаты и в конце кидаем понятную ошибку
      const tried = [devCandidate, exeCandidate, cwdCandidate, srcSnapshotPath];
      throw new Error(`index.html not found. Tried: ${tried.join(', ')}`);
    }
  }

  const indexPath = path.join(staticDir, 'index.html');

  const server = http.createServer((req, res) => {
    // CORS headers for dev mode (frontend on different port)
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

    // Handle preflight
    if (req.method === 'OPTIONS') {
      res.writeHead(204);
      res.end();
      return;
    }

    let pathname;
    try {
      pathname = decodeURIComponent(new URL(req.url, `http://${req.headers.host}`).pathname);
    } catch (e) {
      pathname = req.url || '/';
    }

    // Debug logging for API requests
    if (pathname.startsWith('/api/')) {
      console.log(`[Server] API Request: ${req.method} ${pathname}`);
    }

    // API: GET /api/styles - load styles
    if (pathname === '/api/styles' && req.method === 'GET') {
      const styles = loadStyles();
      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify(styles));
      console.log('[API] GET /api/styles');
      return;
    }

    // API: POST /api/styles - save styles
    if (pathname === '/api/styles' && req.method === 'POST') {
      let body = '';
      req.on('data', chunk => { body += chunk.toString(); });
      req.on('end', () => {
        try {
          const styles = JSON.parse(body);
          const success = saveStyles(styles);
          res.writeHead(success ? 200 : 500, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ success }));
          console.log('[API] POST /api/styles', success ? 'OK' : 'FAILED');
        } catch (e) {
          res.writeHead(400, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ error: 'Invalid JSON' }));
        }
      });
      return;
    }

    // API: GET /api/css/:playerName - load CSS file
    const cssGetMatch = pathname.match(/^\/api\/css\/(\w+)$/);
    if (cssGetMatch && req.method === 'GET') {
      const playerName = cssGetMatch[1];
      const cssFilePath = path.join(cssDir, `${playerName}.css`);
      console.log(`[API] GET /api/css/${playerName} -> ${cssFilePath}`);

      try {
        if (fs.existsSync(cssFilePath)) {
          const cssContent = fs.readFileSync(cssFilePath, 'utf-8');
          res.writeHead(200, { 'Content-Type': 'text/css; charset=utf-8' });
          res.end(cssContent);
        } else {
          // Return empty string if file doesn't exist
          res.writeHead(200, { 'Content-Type': 'text/css; charset=utf-8' });
          res.end('');
        }
      } catch (e) {
        console.error(`[API] Error reading CSS for ${playerName}:`, e.message);
        res.writeHead(500);
        res.end('Server error');
      }
      return;
    }

    // API: POST /api/css/:playerName - save CSS file
    const cssPostMatch = pathname.match(/^\/api\/css\/(\w+)$/);
    if (cssPostMatch && req.method === 'POST') {
      const playerName = cssPostMatch[1];
      const cssFilePath = path.join(cssDir, `${playerName}.css`);

      let body = '';
      req.on('data', chunk => { body += chunk.toString(); });
      req.on('end', () => {
        try {
          ensureDirSync(cssDir);
          fs.writeFileSync(cssFilePath, body, 'utf-8');
          console.log(`[API] POST /api/css/${playerName} -> saved to ${cssFilePath}`);
          res.writeHead(200, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ success: true, path: cssFilePath }));
        } catch (e) {
          console.error(`[API] Error saving CSS for ${playerName}:`, e.message);
          res.writeHead(500, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ success: false, error: e.message }));
        }
      });
      return;
    }

    // API: GET /api/open-css/:playerName - open CSS file in default editor
    const openCssMatch = pathname.match(/^\/api\/open-css\/(\w+)$/);
    if (openCssMatch && req.method === 'GET') {
      const playerName = openCssMatch[1];
      const cssFilePath = path.join(cssDir, `${playerName}.css`);

      try {
        ensureDirSync(cssDir);
        // Create file if doesn't exist
        if (!fs.existsSync(cssFilePath)) {
          fs.writeFileSync(cssFilePath, '', 'utf-8');
        }
        // Open in default editor
        const { exec } = require('child_process');
        exec(`start "" "${cssFilePath}"`, (err) => {
          if (err) {
            console.error(`[API] Error opening CSS file:`, err.message);
          }
        });
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ success: true, path: cssFilePath }));
      } catch (e) {
        console.error(`[API] Error:`, e.message);
        res.writeHead(500, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ success: false, error: e.message }));
      }
      return;
    }

    // ========================================
    // CUSTOM PLAYERS API
    // ========================================

    // Debug: log all /api/custom-players requests
    if (pathname.startsWith('/api/custom-players')) {
      console.log(`[DEBUG] Custom players request: method=${req.method} pathname="${pathname}" exact=${pathname === '/api/custom-players'}`);
    }

    // API: GET /api/custom-players - list all custom players
    if (pathname === '/api/custom-players' && req.method === 'GET') {
      try {
        ensureDirSync(customPlayersDir);
        const files = fs.readdirSync(customPlayersDir);
        const players = files
          .filter(f => f.endsWith('.html') && !f.endsWith('.backup.html'))
          .map(f => {
            const name = f.replace('.html', '');
            const htmlPath = path.join(customPlayersDir, f);
            const backupPath = path.join(customPlayersDir, `${name}.backup.html`);
            return {
              name,
              hasBackup: fs.existsSync(backupPath),
              isCustom: true
            };
          });
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ players }));
        console.log(`[API] GET /api/custom-players -> ${players.length} players`);
      } catch (e) {
        console.error('[API] Error listing custom players:', e.message);
        res.writeHead(500, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ error: e.message }));
      }
      return;
    }

    // API: POST /api/custom-players/validate - validate HTML without saving
    if (pathname === '/api/custom-players/validate' && req.method === 'POST') {
      let body = '';
      req.on('data', chunk => { body += chunk.toString(); });
      req.on('end', () => {
        try {
          const { html } = JSON.parse(body);
          const result = validateHTML(html);
          res.writeHead(200, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify(result));
          console.log(`[API] POST /api/custom-players/validate -> valid: ${result.valid}`);
        } catch (e) {
          res.writeHead(400, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ error: 'Invalid JSON' }));
        }
      });
      return;
    }

    // API: POST /api/custom-players - upload new custom player
    if (pathname === '/api/custom-players' && req.method === 'POST') {
      let body = '';
      req.on('data', chunk => { body += chunk.toString(); });
      req.on('end', () => {
        try {
          const { name, html } = JSON.parse(body);

          if (!name || !html) {
            res.writeHead(400, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ error: 'Name and HTML are required' }));
            return;
          }

          // Sanitize name (alphanumeric, dash, underscore only)
          const safeName = name.replace(/[^a-zA-Z0-9_-]/g, '');
          if (!safeName) {
            res.writeHead(400, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ error: 'Invalid player name' }));
            return;
          }

          // Validate HTML
          const validation = validateHTML(html);
          if (!validation.valid) {
            res.writeHead(400, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({
              error: 'HTML validation failed',
              validation
            }));
            return;
          }

          // Save files
          ensureDirSync(customPlayersDir);
          const htmlPath = path.join(customPlayersDir, `${safeName}.html`);
          const backupPath = path.join(customPlayersDir, `${safeName}.backup.html`);

          // Save main file
          fs.writeFileSync(htmlPath, html, 'utf-8');

          // Save backup (only on first upload, not overwrite)
          if (!fs.existsSync(backupPath)) {
            fs.writeFileSync(backupPath, html, 'utf-8');
          }

          res.writeHead(200, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({
            success: true,
            name: safeName,
            path: htmlPath,
            validation
          }));
          console.log(`[API] POST /api/custom-players -> saved ${safeName}`);
        } catch (e) {
          console.error('[API] Error saving custom player:', e.message);
          res.writeHead(500, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ error: e.message }));
        }
      });
      return;
    }

    // API: GET /api/custom-players/:name - get HTML content
    const customGetMatch = pathname.match(/^\/api\/custom-players\/([a-zA-Z0-9_-]+)$/);
    if (customGetMatch && req.method === 'GET') {
      const playerName = customGetMatch[1];
      const htmlPath = path.join(customPlayersDir, `${playerName}.html`);

      try {
        if (fs.existsSync(htmlPath)) {
          const html = fs.readFileSync(htmlPath, 'utf-8');
          res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
          res.end(html);
          console.log(`[API] GET /api/custom-players/${playerName}`);
        } else {
          res.writeHead(404, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ error: 'Player not found' }));
        }
      } catch (e) {
        console.error(`[API] Error reading custom player ${playerName}:`, e.message);
        res.writeHead(500, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ error: e.message }));
      }
      return;
    }

    // API: PUT /api/custom-players/:name - update HTML (EDIT)
    const customPutMatch = pathname.match(/^\/api\/custom-players\/([a-zA-Z0-9_-]+)$/);
    if (customPutMatch && req.method === 'PUT') {
      const playerName = customPutMatch[1];
      const htmlPath = path.join(customPlayersDir, `${playerName}.html`);

      let body = '';
      req.on('data', chunk => { body += chunk.toString(); });
      req.on('end', () => {
        try {
          const { html } = JSON.parse(body);

          if (!html) {
            res.writeHead(400, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ error: 'HTML is required' }));
            return;
          }

          // Check if player exists
          if (!fs.existsSync(htmlPath)) {
            res.writeHead(404, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ error: 'Player not found' }));
            return;
          }

          // Validate HTML
          const validation = validateHTML(html);
          if (!validation.valid) {
            res.writeHead(400, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({
              error: 'HTML validation failed',
              validation
            }));
            return;
          }

          // Update file (don't touch backup)
          fs.writeFileSync(htmlPath, html, 'utf-8');

          res.writeHead(200, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({
            success: true,
            name: playerName,
            validation
          }));
          console.log(`[API] PUT /api/custom-players/${playerName} -> updated`);
        } catch (e) {
          console.error(`[API] Error updating custom player ${playerName}:`, e.message);
          res.writeHead(500, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ error: e.message }));
        }
      });
      return;
    }

    // API: DELETE /api/custom-players/:name - delete player
    const customDeleteMatch = pathname.match(/^\/api\/custom-players\/([a-zA-Z0-9_-]+)$/);
    if (customDeleteMatch && req.method === 'DELETE') {
      const playerName = customDeleteMatch[1];
      const htmlPath = path.join(customPlayersDir, `${playerName}.html`);
      const backupPath = path.join(customPlayersDir, `${playerName}.backup.html`);
      const cssPath = path.join(cssDir, `${playerName}.css`);

      try {
        let deleted = false;

        if (fs.existsSync(htmlPath)) {
          fs.unlinkSync(htmlPath);
          deleted = true;
        }
        if (fs.existsSync(backupPath)) {
          fs.unlinkSync(backupPath);
        }
        if (fs.existsSync(cssPath)) {
          fs.unlinkSync(cssPath);
        }

        if (deleted) {
          res.writeHead(200, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ success: true, name: playerName }));
          console.log(`[API] DELETE /api/custom-players/${playerName}`);
        } else {
          res.writeHead(404, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ error: 'Player not found' }));
        }
      } catch (e) {
        console.error(`[API] Error deleting custom player ${playerName}:`, e.message);
        res.writeHead(500, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ error: e.message }));
      }
      return;
    }

    // API: GET /api/custom-players/:name/backup - get backup HTML (for RESET)
    const customBackupMatch = pathname.match(/^\/api\/custom-players\/([a-zA-Z0-9_-]+)\/backup$/);
    if (customBackupMatch && req.method === 'GET') {
      const playerName = customBackupMatch[1];
      const backupPath = path.join(customPlayersDir, `${playerName}.backup.html`);

      try {
        if (fs.existsSync(backupPath)) {
          const html = fs.readFileSync(backupPath, 'utf-8');
          res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
          res.end(html);
          console.log(`[API] GET /api/custom-players/${playerName}/backup`);
        } else {
          res.writeHead(404, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ error: 'Backup not found' }));
        }
      } catch (e) {
        console.error(`[API] Error reading backup for ${playerName}:`, e.message);
        res.writeHead(500, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ error: e.message }));
      }
      return;
    }

    // API: POST /api/custom-players/:name/reset - reset to backup
    const customResetMatch = pathname.match(/^\/api\/custom-players\/([a-zA-Z0-9_-]+)\/reset$/);
    if (customResetMatch && req.method === 'POST') {
      const playerName = customResetMatch[1];
      const htmlPath = path.join(customPlayersDir, `${playerName}.html`);
      const backupPath = path.join(customPlayersDir, `${playerName}.backup.html`);

      try {
        if (!fs.existsSync(backupPath)) {
          res.writeHead(404, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ error: 'Backup not found' }));
          return;
        }

        // Copy backup to main file
        const backupContent = fs.readFileSync(backupPath, 'utf-8');
        fs.writeFileSync(htmlPath, backupContent, 'utf-8');

        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ success: true, name: playerName }));
        console.log(`[API] POST /api/custom-players/${playerName}/reset -> restored from backup`);
      } catch (e) {
        console.error(`[API] Error resetting custom player ${playerName}:`, e.message);
        res.writeHead(500, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ error: e.message }));
      }
      return;
    }

    // ========================================
    // MEDIA FILTER API
    // ========================================

    // API: GET /api/media-filter - get filter config
    if (pathname === '/api/media-filter' && req.method === 'GET') {
      const config = loadMediaFilter();
      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify(config));
      console.log('[API] GET /api/media-filter');
      return;
    }

    // API: POST /api/media-filter - save filter config
    if (pathname === '/api/media-filter' && req.method === 'POST') {
      let body = '';
      req.on('data', chunk => { body += chunk.toString(); });
      req.on('end', () => {
        try {
          const config = JSON.parse(body);
          const success = saveMediaFilter(config);
          reloadFilter();
          res.writeHead(success ? 200 : 500, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ success }));
          console.log('[API] POST /api/media-filter', success ? 'OK' : 'FAILED');
        } catch (e) {
          res.writeHead(400, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ error: 'Invalid JSON' }));
        }
      });
      return;
    }

    if (pathname === '/') pathname = '/index.html';

    const requested = path.normalize(path.join(staticDir, pathname));
    if (!requested.startsWith(staticDir)) {
      console.log(`[FrontendServer] 403 Forbidden: ${pathname}`);
      res.writeHead(403);
      res.end('Forbidden');
      return;
    }

    fs.stat(requested, (err, stats) => {
      let toServe = requested;
      if (err || !stats.isFile()) {
        // Если файл не найден, возвращаем index.html (SPA routing)
        toServe = indexPath;
        console.log(`[FrontendServer] File not found, serving index: ${pathname}`);
      }

      fs.readFile(toServe, (readErr, data) => {
        if (readErr) {
          console.error(`[FrontendServer] 500 Error reading ${toServe}:`, readErr.message);
          res.writeHead(500);
          res.end('Server error');
          return;
        }
        const ext = path.extname(toServe).slice(1).toLowerCase();
        const mime = mimeMap[ext] || 'application/octet-stream';

        console.log(`[FrontendServer] 200 ${pathname} -> ${mime}`);
        res.writeHead(200, { 'Content-Type': mime });
        res.end(data);
      });
    });
  });

  server.listen(port, '127.0.0.1', () => {
    console.log(`[FrontendServer] serving ${staticDir}`);
    console.log(`[FrontendServer] running at http://localhost:${port}/`);
  });

  return server;
}

module.exports = { startFrontendServer }