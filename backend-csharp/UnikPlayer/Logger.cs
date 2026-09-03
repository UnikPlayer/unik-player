using System.Text;

namespace UnikPlayer;

public class DualWriter : TextWriter
{
    private readonly TextWriter _console;
    private readonly string _logDir;
    private StreamWriter? _file;
    private string? _currentDate;

    public override Encoding Encoding => Encoding.UTF8;

    public DualWriter(TextWriter consoleOut, string logDir)
    {
        _console = consoleOut;
        _logDir = logDir;
        Directory.CreateDirectory(logDir);
        CleanOldLogs();
    }

    private StreamWriter GetFile()
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        if (_file == null || _currentDate != today)
        {
            _file?.Dispose();
            _currentDate = today;
            var path = Path.Combine(_logDir, $"app-{today}.log");
            _file = new StreamWriter(path, append: true, Encoding.UTF8)
                { AutoFlush = true };
        }
        return _file;
    }

    public override void WriteLine(string? value)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {value}";
        _console.WriteLine(line);
        try { GetFile().WriteLine(line); } catch { }
    }

    public override void Write(char value) => _console.Write(value);
    public override void Write(string? value) => _console.Write(value);

    protected override void Dispose(bool disposing)
    {
        if (disposing) _file?.Dispose();
        base.Dispose(disposing);
    }

    private void CleanOldLogs()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-30);
            foreach (var file in Directory.GetFiles(_logDir, "app-*.log"))
            {
                var name = Path.GetFileNameWithoutExtension(file).Replace("app-", "");
                if (DateTime.TryParse(name, out var date) && date < cutoff)
                    File.Delete(file);
            }
        }
        catch { }
    }
}

static class Logger
{
    public static void SessionStart() => WriteLine("INFO", "Session started");
    public static void SessionEnd() => WriteLine("INFO", "Session ended");
    public static void SetBackendState(string state) => WriteLine("INFO", $"Backend state: {state}");
    public static void Warning(string message) => WriteLine("WARN", message);
    public static void Error(string message) => WriteLine("ERROR", message);

    private static void WriteLine(string tag, string message)
    {
        Console.WriteLine($"[{tag}] {message}");
    }
}
