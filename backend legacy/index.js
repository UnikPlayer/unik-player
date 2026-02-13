const { SMTCListener } = require('./funcs/SMTC.js');
const { makingAppDataDir } = require('./funcs/folder.js');
const { openSite } = require('./funcs/openLocalSite.js');
const { startFrontendServer } = require('./funcs/server.js');
const { systemTray } = require('./funcs/tray.js');
const { checkSingleInstance } = require('./funcs/lockFile.js');
const { hideConsole } = require('./funcs/hideConsole.js');
const { setupErrorHandlers } = require('./funcs/errorHandler.js');

const isAutostart = process.argv.includes('--autostart');

console.log('UnikPlayer запускается...');
if (isAutostart) {
  console.log('Режим автозапуска: браузер не будет открыт');
}

if (!checkSingleInstance()) return;

setupErrorHandlers();

//НЕ РАБОТАЕТ makingAppDataDir()
SMTCListener();
console.log('SMTC слушатель запущен');

systemTray();
console.log('Системный трей запущен');

startFrontendServer({ port: 27272 });
console.log('Фронтенд сервер запущен на порту 27272');

if (!isAutostart) {
  openSite();
  console.log('Сайт открыт в браузере');
}

console.log('UnikPlayer работает. Нажмите Ctrl+C для выхода.');

hideConsole();
