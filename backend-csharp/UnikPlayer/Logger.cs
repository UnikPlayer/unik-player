using System.Text;

namespace UnikPlayer;

/// <summary>
/// TextWriter that writes to both the original Console and a daily log file.
/// All Console.WriteLine output automatically goes to the log file.
/// </summary>
public class DualWriter : TextWriter
{
    private readonly TextWriter _originalOut;
    private readonly string _logDir;
    private string? _currentLogPath;
    private StreamWriter? _fileWriter;
    private string? _lastDay;

    public override Encoding Encoding => Encoding.UTF8;

    public DualWriter(TextWriter originalOut, string logDir)
    {
        _originalOut = originalOut;
        _logDir = logDir;
        Directory.CreateDirectory(_logDir);
        CleanOldLogs();
    }

    private string GetLogPath()
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        if (_currentLogPath == null || _lastDay != today)
        {
            _lastDay = today;
            _currentLogPath = Path.Combine(_logDir, $"app-{today}.log");
        }
        return _currentLogPath;
    }

    private StreamWriter GetWriter()
    {
        var path = GetLogPath();
        if (_fileWriter == null || _lastDay != DateTime.Now.ToString("yyyy-MM-dd"))
        {
            _fileWriter?.Dispose();
            _fileWriter = new StreamWriter(
                File.Open(path, FileMode.Append, FileAccess.Write, FileShare.Read),
                Encoding.UTF8)
            { AutoFlush = true };
        }
        return _fileWriter;
    }

    private static string Timestamp() =>
        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

    public override void Write(char value)
    {
        _originalOut.Write(value);
        try { GetWriter().Write(value); } catch { }
    }

    public override void Write(string? value)
    {
        _originalOut.Write(value);
        try { GetWriter().Write(value); } catch { }
    }

    public override void WriteLine(string? value)
    {
        _originalOut.WriteLine(value);
        try { GetWriter().WriteLine($"[{Timestamp()}] {value}"); } catch { }
    }

    public override void WriteLine(string format, params object?[] args)
    {
        var msg = string.Format(format, args);
        _originalOut.WriteLine(msg);
        try { GetWriter().WriteLine($"[{Timestamp()}] {msg}"); } catch { }
    }

    public override void Write(char[] buffer, int index, int count)
    {
        _originalOut.Write(buffer, index, count);
        try { GetWriter().Write(buffer, index, count); } catch { }
    }

    public override void Flush()
    {
        _originalOut.Flush();
        try { _fileWriter?.Flush(); } catch { }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fileWriter?.Dispose();
        }
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
                if (DateTime.TryParse(name, out var fileDate) && fileDate < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch { }
    }
}

/// <summary>
/// Static logger with tag-based convenience methods.
/// Uses Console.WriteLine (which is redirected to DualWriter -> file).
/// </summary>
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
