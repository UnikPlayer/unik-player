const { exec } = require('child_process');

function hideConsole(delay = 2000) {
  setTimeout(() => {
    try {
      const script = `
        Add-Type -Name Window -Namespace Console -MemberDefinition '
        [DllImport("Kernel32.dll")]
        public static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, Int32 nCmdShow);
        ';
        $consolePtr = [Console.Window]::GetConsoleWindow();
        [Console.Window]::ShowWindow($consolePtr, 0);
      `;

      exec(`powershell -WindowStyle Hidden -Command "${script.replace(/\n/g, ' ')}"`, (err) => {
        if (!err) {
          console.log('Консоль скрыта');
        }
      });
    } catch (err) {
      // Консоль останется видимой, но приложение продолжит работу
    }
  }, delay);
}

module.exports = { hideConsole };
