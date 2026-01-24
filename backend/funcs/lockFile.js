const fs = require('fs');
const path = require('path');

const lockFile = path.join(process.env.TEMP || '/tmp', 'unikplayer.lock');

// Проверяет, существует ли процесс с данным PID
function isProcessRunning(pid) {
  try {
    process.kill(pid, 0);
    return true;
  } catch (e) {
    return false;
  }
}

// Проверка на единственный экземпляр
function checkSingleInstance() {
  try {
    if (fs.existsSync(lockFile)) {
      const pid = parseInt(fs.readFileSync(lockFile, 'utf8'), 10);

      if (isProcessRunning(pid)) {
        console.log(`UnikPlayer уже запущен (PID: ${pid})`);
        console.log('Завершение работы...');
        setTimeout(() => process.exit(0), 1000);
        return false;
      } else {
        console.log(`Старый процесс (PID: ${pid}) не найден, удаляю lock файл...`);
        fs.unlinkSync(lockFile);
      }
    }

    // Создаем lock-файл с текущим PID
    fs.writeFileSync(lockFile, String(process.pid));

    // Удаляем lock-файл при выходе
    process.on('exit', () => {
      try {
        if (fs.existsSync(lockFile)) {
          fs.unlinkSync(lockFile);
        }
      } catch (e) {}
    });

    return true;
  } catch (e) {
    console.error('Ошибка проверки единственного экземпляра:', e.message);
    return true;
  }
}

module.exports = { checkSingleInstance };
