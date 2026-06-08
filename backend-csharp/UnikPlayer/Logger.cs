namespace UnikPlayer;

static class Logger
{
    public static void SessionStart() => Console.WriteLine("[Logger] Session started");
    public static void SessionEnd() => Console.WriteLine("[Logger] Session ended");
    public static void SetBackendState(string state) => Console.WriteLine($"[Logger] Backend state: {state}");
    public static void Warning(string message) => Console.WriteLine($"[Logger] WARNING: {message}");
    public static void Error(string message) => Console.WriteLine($"[Logger] ERROR: {message}");
}
