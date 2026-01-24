function setupErrorHandlers() {
  process.on('uncaughtException', (err) => {
    console.error('Необработанная ошибка:', err);
  });

  process.on('unhandledRejection', (err) => {
    console.error('Необработанное отклонение промиса:', err);
  });
}

module.exports = { setupErrorHandlers };
