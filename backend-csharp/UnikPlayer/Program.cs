using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Net;
using System.Net.Sockets;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Svg;
using Windows.Media.Control;
using Windows.Storage.Streams;
using WindowsMediaController;

namespace UnikPlayer;

class Program
{
    private static readonly List<WebSocket> _clients = new();
    private static readonly object _lock = new();
    private static MediaManager? _mediaManager;
    private static NotifyIcon? _trayIcon;
    private static HttpListener? _httpListener;
    private static HttpListener? _wsListener;
    private static string? _lastFingerprint;
    private static string? _lastSentJson;
    private static DateTime _lastSentTime = DateTime.MinValue;
    private static double _lastSentPosition = 0;  // Last sent timeline position
    private static string? _activeSessionId;  // ID ��������� �������� ������
    private static double _knownPosition = 0;      // ������� �� SMTC ��� ��������� ����������
    private static DateTime _knownPositionTime = DateTime.MinValue;  // ����� �������� ��� �������
    private static double _knownDuration = 0;      // ������������ �����
    private static readonly int DEBOUNCE_MS = 300;
    private static readonly double POSITION_THRESHOLD = 2.0;  // Send update if position differs by more than 2 seconds
    private static readonly int HTTP_PORT = 27272;
    private static readonly int WS_PORT = 62727;
    private static System.Threading.Timer? _positionTimer;  // Timer for sending position every second

    // Data paths - initialized in Main based on DEV_MODE
    private static string STYLES_DIR = "";
    private static string STYLES_FILE = "";
    private static string CSS_DIR = "";
    private static string CUSTOM_PLAYERS_DIR = "";
    private static bool DEV_MODE = false;
    private static bool NO_FRONTEND = false;

    // Media filter
    private static string MEDIA_FILTER_FILE = "";
    private static MediaFilterConfig _mediaFilter = new();
    private static readonly object _filterLock = new();

    // Site auth (cloud sync)
    private static string SITE_AUTH_FILE = "";
    private static string? _siteAuthToken;
    private static string? _siteAuthNickname;

    // Frontend logs storage
    private static readonly List<string> _frontendLogs = new();
    private static readonly object _frontendLogsLock = new();

    // Auto-update
    private static readonly HttpClient _httpUpdate = new();
    private static bool _updateAvailable = false;
    private static string? _latestInstallerUrl;
    private static string? _latestVersion;
    private static string? _currentVersion;
    private static ContextMenuStrip? _trayMenu;
    private static ToolStripMenuItem? _updateMenuItem;
    private static readonly string GITHUB_API_URL = "https://api.github.com/repos/UNIKNOW0/unik-player/releases/latest";

    class MediaFilterConfig
    {
        public string mode { get; set; } = "allowAll";
        public List<string> sources { get; set; } = new();
        public List<string> seenSources { get; set; } = new();
    }

    static MediaFilterConfig LoadMediaFilter()
    {
        lock (_filterLock)
        {
            try
            {
                if (File.Exists(MEDIA_FILTER_FILE))
                {
                    var json = File.ReadAllText(MEDIA_FILTER_FILE);
                    var config = JsonSerializer.Deserialize<MediaFilterConfig>(json);
                    if (config != null)
                    {
                        _mediaFilter = config;
                        return _mediaFilter;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MediaFilter] Load error: {ex.Message}");
            }
            _mediaFilter = new MediaFilterConfig();
            return _mediaFilter;
        }
    }

    static bool SaveMediaFilter(MediaFilterConfig config)
    {
        lock (_filterLock)
        {
            try
            {
                Directory.CreateDirectory(STYLES_DIR);
                _mediaFilter = config;
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(MEDIA_FILTER_FILE, json);
                Console.WriteLine("[MediaFilter] Config saved");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MediaFilter] Save error: {ex.Message}");
                return false;
            }
        }
    }

    static void AddSeenSource(string sourceAppId)
    {
        if (string.IsNullOrWhiteSpace(sourceAppId)) return;

        lock (_filterLock)
        {
            if (!_mediaFilter.seenSources.Contains(sourceAppId))
            {
                _mediaFilter.seenSources.Add(sourceAppId);
                SaveMediaFilter(_mediaFilter);
                Console.WriteLine($"[MediaFilter] New source seen: {sourceAppId}");
            }
        }
    }

    static bool ShouldAllowSource(string sourceAppId)
    {
        lock (_filterLock)
        {
            return _mediaFilter.mode switch
            {
                "allowOnly" => _mediaFilter.sources.Contains(sourceAppId),
                "blockOnly" => !_mediaFilter.sources.Contains(sourceAppId),
                _ => true // allowAll
            };
        }
    }

    /// <summary>
    /// Get display name for app from SourceAppUserModelId
    /// </summary>
    static string GetAppDisplayName(string appId)
    {
        if (string.IsNullOrEmpty(appId)) return "Unknown";

        try
        {
            // Handle exe path: "C:\...\Spotify.exe" -> "Spotify"
            if (appId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                var name = Path.GetFileNameWithoutExtension(appId);
                if (!string.IsNullOrEmpty(name))
                    return name;
            }

            // Handle GUID (browser): "{308046B0AF4A39CB}" -> "Browser"
            if (appId.StartsWith("{") && appId.EndsWith("}"))
            {
                return "Browser";
            }

            // Handle AUMID: "Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic" -> "ZuneMusic"
            if (appId.Contains('!'))
            {
                var afterBang = appId.Split('!').Last();
                var cleanName = afterBang.Replace("Microsoft.", "").Replace("App", "");
                if (!string.IsNullOrEmpty(cleanName))
                    return cleanName;
            }

            // Handle package name: "com.github.th-ch.youtube-music" -> "YouTube Music"
            if (appId.Contains('.'))
            {
                var parts = appId.Split('.');
                var lastPart = parts.Last();
                // Convert camelCase to readable: "youtube-music" -> "YouTube Music"
                var readable = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
                    lastPart.Replace("-", " ")
                );
                if (!string.IsNullOrEmpty(readable))
                    return readable;
            }

            // Fallback: just return the last part after underscore or the full id
            if (appId.Contains('_'))
            {
                var beforeUnderscore = appId.Split('_')[0];
                var cleanName = beforeUnderscore.Replace("Microsoft.", "");
                if (!string.IsNullOrEmpty(cleanName))
                    return cleanName;
            }
        }
        catch { }

        return appId;
    }

    /// <summary>
    /// Load .env file and return dictionary of key-value pairs
    /// </summary>
    static Dictionary<string, string> LoadEnvFile()
    {
        var env = new Dictionary<string, string>();
        var envPaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, ".env"),
            Path.Combine(Directory.GetCurrentDirectory(), ".env"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".env"),
        };

        foreach (var envPath in envPaths)
        {
            if (File.Exists(envPath))
            {
                Console.WriteLine($"[Config] Loading .env from: {envPath}");
                foreach (var line in File.ReadAllLines(envPath))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

                    var eqIndex = trimmed.IndexOf('=');
                    if (eqIndex > 0)
                    {
                        var key = trimmed.Substring(0, eqIndex).Trim();
                        var value = trimmed.Substring(eqIndex + 1).Trim();
                        env[key] = value;
                    }
                }
                break;
            }
        }

        return env;
    }

    /// <summary>
    /// Initialize data paths based on DEV_MODE
    /// </summary>
    static void InitializePaths()
    {
        var env = LoadEnvFile();

        DEV_MODE = env.TryGetValue("DEV_MODE", out var devMode) &&
                   devMode.Equals("true", StringComparison.OrdinalIgnoreCase);

        NO_FRONTEND = env.TryGetValue("NO_FRONTEND", out var noFront) &&
                      noFront.Equals("true", StringComparison.OrdinalIgnoreCase);

        if (DEV_MODE)
        {
            // Dev mode: use local paths relative to project
            var devDataDir = env.TryGetValue("DEV_DATA_DIR", out var dir) ? dir : "../../dev-data";
            var basePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, devDataDir));

            // Also try relative to current directory for `dotnet run`
            if (!Directory.Exists(basePath))
            {
                basePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), devDataDir));
            }

            STYLES_DIR = basePath;
            Console.WriteLine($"[Config] DEV MODE - Data directory: {STYLES_DIR}");
        }
        else
        {
            // Production mode: use %LOCALAPPDATA%
            STYLES_DIR = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UnikPlayer"
            );
            Console.WriteLine($"[Config] PRODUCTION MODE - Data directory: {STYLES_DIR}");
        }

        STYLES_FILE = Path.Combine(STYLES_DIR, "player-styles.json");
        CSS_DIR = Path.Combine(STYLES_DIR, "css");
        CUSTOM_PLAYERS_DIR = Path.Combine(STYLES_DIR, "custom");
        MEDIA_FILTER_FILE = Path.Combine(STYLES_DIR, "media-filter.json");
        SITE_AUTH_FILE = Path.Combine(STYLES_DIR, "site-auth.json");

        // Ensure directories exist
        Directory.CreateDirectory(STYLES_DIR);
        Directory.CreateDirectory(CSS_DIR);
        Directory.CreateDirectory(CUSTOM_PLAYERS_DIR);

        // Load media filter config
        LoadMediaFilter();

        // Load site auth state
        LoadSiteAuth();

        // Install example players on first run
        InstallExamplePlayers();
    }

    /// <summary>
    /// Locate the example-players source directory (next to the .exe in production,
    /// or in the project source tree when running via `dotnet run`).
    /// </summary>
     static string? FindExamplePlayersDir()
     {
         var candidates = new List<string>();

         // Approach 1: Use DEV_DATA_DIR if in dev mode
         var env = LoadEnvFile();
         if (DEV_MODE && env.TryGetValue("DEV_DATA_DIR", out var devDataDir))
         {
             var devPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, devDataDir, "..", "frontend", "static", "examples"));
             candidates.Add(devPath);
         }

         // Approach 2: Try to find unikPlayer directory by going up from AppContext.BaseDirectory
         // Structure: .../unikPlayer/backend-csharp/UnikPlayer/bin/Debug/.../win-x64/
         // Target:    .../unikPlayer/frontend/static/examples/
         var baseDir = AppContext.BaseDirectory;
         for (int up = 1; up <= 8; up++)
         {
             var path = baseDir;
             for (int i = 0; i < up; i++)
                 path = Path.GetDirectoryName(path) ?? path;
             
             // Check if this is the unikPlayer root (has frontend folder)
             var testUnik = Path.Combine(path, "frontend", "static", "examples");
             candidates.Add(testUnik);
             
             // Also check unikPlayer subfolder
             var testUnikSub = Path.Combine(path, "unikPlayer", "frontend", "static", "examples");
             candidates.Add(testUnikSub);
         }

         // Approach 3: Relative to current directory
         var curDir = Directory.GetCurrentDirectory();
         candidates.Add(Path.Combine(curDir, "frontend", "static", "examples"));
         candidates.Add(Path.Combine(curDir, "..", "frontend", "static", "examples"));
         candidates.Add(Path.Combine(curDir, "..", "..", "frontend", "static", "examples"));
         candidates.Add(Path.Combine(curDir, "unikPlayer", "frontend", "static", "examples"));

         // Approach 4: Absolute path (most reliable for your setup)
         candidates.Add(@"C:\Users\000-d\Desktop\JShit\Unik player reps\unikPlayer\frontend\static\examples");

         // Legacy example-players directory (backend) - fallback
         candidates.Add(Path.Combine(baseDir, "example-players"));
         candidates.Add(Path.Combine(curDir, "example-players"));

         foreach (var p in candidates)
         {
             if (string.IsNullOrEmpty(p)) continue;
             var full = Path.GetFullPath(p);
             if (Directory.Exists(full))
             {
                 Console.WriteLine($"[Examples] Found examples directory: {full}");
                 return full;
             }
         }
         return null;
     }

    /// <summary>
    /// Install bundled example players from `example-players/*.html`.
    /// `.backup.html` is always refreshed with the latest factory version,
    /// `.html` (user's editable copy) is created only if missing.
    /// </summary>
    static void InstallExamplePlayers()
    {
        try
        {
            var sourceDir = FindExamplePlayersDir();
            if (sourceDir == null)
            {
                Console.WriteLine("[Examples] example-players directory not found, skipping install");
                return;
            }

            int created = 0, refreshed = 0;
            foreach (var srcPath in Directory.GetFiles(sourceDir, "*.html"))
            {
                var name = Path.GetFileNameWithoutExtension(srcPath);
                if (string.IsNullOrEmpty(name)) continue;

                var content = File.ReadAllText(srcPath);
                var htmlPath = Path.Combine(CUSTOM_PLAYERS_DIR, $"{name}.html");
                var backupPath = Path.Combine(CUSTOM_PLAYERS_DIR, $"{name}.backup.html");

                // Always refresh backup with latest factory version (used by Reset)
                File.WriteAllText(backupPath, content);
                refreshed++;

                // Create user's editable copy only if missing
                if (!File.Exists(htmlPath))
                {
                    File.WriteAllText(htmlPath, content);
                    created++;
                    Console.WriteLine($"[Examples] Installed new player: {name}");
                }
            }

            Console.WriteLine($"[Examples] Refreshed {refreshed} backup(s), created {created} new player(s) from {sourceDir}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Examples] Failed to install examples: {ex.Message}");
        }
    }


    [STAThread]
    static void Main(string[] args)
    {
        Logger.SessionStart();
        
        // Single instance check (skip in dev mode)
        bool createdNew;
        using var mutex = new Mutex(true, "UnikPlayer_SingleInstance", out createdNew);

        // Initialize paths first to check DEV_MODE
        InitializePaths();

        // CLI override for no-frontend debug mode
        if (args.Contains("--no-frontend"))
        {
            NO_FRONTEND = true;
        }
        if (NO_FRONTEND)
        {
            Console.WriteLine("[Config] NO_FRONTEND - ������ �� ����� �������� ����� (������ API)");
        }

        if (!createdNew && !DEV_MODE)
        {
            Console.WriteLine("UnikPlayer ��� �������!");
            Logger.Warning("Another instance already running, exiting");
            Logger.SessionEnd();
            return;
        }

        Console.WriteLine("UnikPlayer �����������...");
        Logger.SetBackendState("Starting");

        // Start services
        Task.Run(() => StartHttpServer());
        Task.Run(() => StartWebSocketServer());
        StartMediaManager();

        Console.WriteLine($"HTTP ������: http://localhost:{HTTP_PORT}/");
        Console.WriteLine($"WebSocket ������: ws://localhost:{WS_PORT}/");
        Console.WriteLine("UnikPlayer ��������.");

        // Setup tray icon
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        SetupTrayIcon();

        // Check for updates once at startup, then every 24 hours
        _ = Task.Run(async () =>
        {
            await Task.Delay(60000);
            await CheckForUpdatesAsync();
            while (true)
            {
                await Task.Delay(24 * 60 * 60 * 1000);
                await CheckForUpdatesAsync();
            }
        });

        // Open browser if not autostart and frontend is being served
        if (!args.Contains("--autostart") && !NO_FRONTEND)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = $"http://127.0.0.1:{HTTP_PORT}/",
                UseShellExecute = true
            });
        }

        Application.Run();

        // Cleanup
        _mediaManager?.Dispose();
        _trayIcon?.Dispose();
        _httpListener?.Stop();
        _wsListener?.Stop();
        Logger.SetBackendState("Stopped");
        Logger.SessionEnd();
    }

    static Bitmap? LoadSvgAsBitmap(string filename, int size = 16, bool makeWhite = false)
    {
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, filename),
            Path.Combine(Directory.GetCurrentDirectory(), filename),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", filename),
            Path.Combine(@"C:\Users\000-d\Desktop\JShit\unikPlayer\backend-csharp\UnikPlayer", filename)
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    var svgDoc = SvgDocument.Open(path);

                    // ������ ������ �����
                    if (makeWhite)
                    {
                        SetSvgColor(svgDoc, new SvgColourServer(Color.White));
                    }

                    svgDoc.Width = size;
                    svgDoc.Height = size;
                    var bitmap = svgDoc.Draw(size, size);
                    Console.WriteLine($"[Tray] SVG ��������: {path}");
                    return bitmap;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Tray] ������ �������� SVG {path}: {ex.Message}");
                }
            }
        }

        return null;
    }

    static void SetSvgColor(SvgElement element, SvgPaintServer color)
    {
        if (element.Fill != SvgPaintServer.None && element.Fill != null)
        {
            element.Fill = color;
        }
        if (element.Stroke != SvgPaintServer.None && element.Stroke != null)
        {
            element.Stroke = color;
        }

        foreach (var child in element.Children)
        {
            SetSvgColor(child, color);
        }
    }

    static void SetupTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Text = "UnikPlayer",
            Visible = true
        };

        // ���� ������ � ������ ������
        var possibleIconPaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "icon.ico"),
            Path.Combine(Directory.GetCurrentDirectory(), "icon.ico"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "icon.ico"),
            @"C:\Users\000-d\Desktop\JShit\unikPlayer\backend-csharp\UnikPlayer\icon.ico",
            @"C:\Users\000-d\Desktop\JShit\unikPlayer\projBuild\static\icon.ico"
        };

        Icon? appIcon = null;
        foreach (var path in possibleIconPaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    appIcon = new Icon(path);
                    Console.WriteLine($"[Tray] ������ ���������: {path}");
                    break;
                }
                catch { }
            }
        }

        _trayIcon.Icon = appIcon ?? SystemIcons.Application;

        // ��������� SVG ������ ��� ���� (����� ��� ������ ����)
        var homeIcon = LoadSvgAsBitmap("home.svg", 16, makeWhite: true);
        var exitIcon = LoadSvgAsBitmap("exit.svg", 16, makeWhite: true);

        var menu = new ContextMenuStrip();

        // ������ ���� ��� ����
        menu.Renderer = new DarkMenuRenderer();
        menu.BackColor = Color.FromArgb(32, 32, 32);
        menu.ForeColor = Color.White;

        // ������ "Open site" � �������
        var openItem = new ToolStripMenuItem("Open site", homeIcon, (s, e) =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = $"http://127.0.0.1:{HTTP_PORT}/",
                UseShellExecute = true
            });
        });
        openItem.ForeColor = Color.White;
        menu.Items.Add(openItem);

        menu.Items.Add(new ToolStripSeparator());

        // Кнопка "Обновить приложение" (скрыта по умолчанию)
        _updateMenuItem = new ToolStripMenuItem("Обновить приложение", null, async (s, e) =>
        {
            Console.WriteLine("[Update] User initiated update");
            await StartUpdate(relaunch: true);
        });
        _updateMenuItem.ForeColor = Color.White;
        _updateMenuItem.Visible = false;
        _updateMenuItem.Font = new Font(_updateMenuItem.Font, FontStyle.Bold);
        menu.Items.Add(_updateMenuItem);

        // Кнопка "Exit" с иконкой
        var exitItem = new ToolStripMenuItem("Exit", exitIcon, (s, e) =>
        {
            _trayIcon.Visible = false;
            Application.Exit();
        });
        exitItem.ForeColor = Color.White;
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (s, e) =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = $"http://127.0.0.1:{HTTP_PORT}/",
                UseShellExecute = true
            });
        };
    }

    static void StartMediaManager()
    {
        _mediaManager = new MediaManager();

        _mediaManager.OnAnyMediaPropertyChanged += async (session, args) =>
        {
            // ��������� ������ ���� ��� �������� ������ ��� ��� ��������
            if (_activeSessionId == null || _activeSessionId == session.Id)
            {
                await SendMediaUpdate(session);
            }
        };

        _mediaManager.OnAnyPlaybackStateChanged += async (session, args) =>
        {
            var playback = session.ControlSession.GetPlaybackInfo();

            if (playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                // Track source
                AddSeenSource(session.Id ?? "");

                // Check filter before making active
                if (!ShouldAllowSource(session.Id ?? ""))
                {
                    return;
                }

                // ������ ������ ������ - ������ � ��������
                _activeSessionId = session.Id;
                Console.WriteLine($"[SMTC] �������� ������: {session.Id}");

                // Stop old timer first � SendMediaUpdate will set correct position data
                StopPositionTimer();
                await SendMediaUpdate(session);
                // Now _knownPosition/_knownDuration are correct � start timer
                StartPositionTimer();
            }
            else if (_activeSessionId == session.Id)
            {
                // �������� ������ �� Playing - ������������� ������ � ���� ������
                StopPositionTimer();
                SendPlaybackUpdate(session);
                await FindAndSendPlayingSession();
            }
        };

        // Timeline changes (seek)
        _mediaManager.OnAnyTimelinePropertyChanged += async (session, args) =>
        {
            if (_activeSessionId == session.Id)
            {
                SendTimelineUpdate(session);
            }
        };

        _mediaManager.OnAnySessionOpened += async (session) =>
        {
            // ����� ������ - ��������� ������ �� ���
            var playback = session.ControlSession.GetPlaybackInfo();
            if (playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                _activeSessionId = session.Id;
                StopPositionTimer();
                await SendMediaUpdate(session);
                StartPositionTimer();
            }
        };

        _mediaManager.OnAnySessionClosed += async (session) =>
        {
            if (_activeSessionId == session.Id)
            {
                StopPositionTimer();
                _activeSessionId = null;
                await FindAndSendPlayingSession();
            }
        };

        _mediaManager.Start();
        Console.WriteLine("SMTC ��������� �������");
    }

    static async Task FindAndSendPlayingSession()
    {
        if (_mediaManager == null) return;

        // ���������, �������� �� ������� �������� ��������
        if (_activeSessionId != null && !ShouldAllowSource(_activeSessionId))
        {
            Console.WriteLine($"[SMTC] �������� �������� {_activeSessionId} ������ ������������");
            _activeSessionId = null;
            _lastFingerprint = null;
            _lastSentJson = null;
        }

        // ���� ����� ������ �� �������� Playing (� ������ �������)
        foreach (var session in _mediaManager.CurrentMediaSessions.Values)
        {
            try
            {
                var playback = session.ControlSession.GetPlaybackInfo();
                if (playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                {
                    var sourceId = session.Id ?? "";
                    AddSeenSource(sourceId);

                    if (!ShouldAllowSource(sourceId))
                        continue;

                    _activeSessionId = session.Id;
                    StopPositionTimer();
                    await SendMediaUpdate(session);
                    StartPositionTimer();
                    return;
                }
            }
            catch { }
        }

        // ��� Playing ������ - ��� ������� (����� ���� ����� �����)
        await Task.Delay(300);

        // ��������� ��� ��� - ����� ��������� Playing ������
        foreach (var session in _mediaManager.CurrentMediaSessions.Values)
        {
            try
            {
                var playback = session.ControlSession.GetPlaybackInfo();
                if (playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                {
                    var sourceId = session.Id ?? "";
                    if (ShouldAllowSource(sourceId))
                    {
                        // ����� Playing - �� ��������, SendMediaUpdate ��������� �� �������
                        return;
                    }
                }
            }
            catch { }
        }

        // ����� �������� �� ��� ��� Playing - ��������
        _activeSessionId = null;
        _lastFingerprint = null;
        _lastSentJson = null;
        BroadcastMessage(JsonSerializer.Serialize(new { media = (object?)null }));
        Console.WriteLine("[SMTC] ��� Playing ������ - �������� �����");
    }

    static async Task SendMediaUpdate(MediaManager.MediaSession session, int retryCount = 0)
    {
        const int RETRY_DELAY_MS = 200;
        const int MAX_RETRIES = 2;  // 2 * 200ms = 400ms max ��� ��������
        try
        {
            // Track seen source and check filter
            var sourceId = session.Id ?? "";
            AddSeenSource(sourceId);

            if (!ShouldAllowSource(sourceId))
            {
                // Source is filtered out � clear active and let caller handle
                if (_activeSessionId == session.Id)
                {
                    _activeSessionId = null;
                }
                return;
            }

            var playback = session.ControlSession.GetPlaybackInfo();

            // ���� ������ �� Playing - ������� (FindAndSendPlayingSession ����� Playing ������)
            if (playback.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                Console.WriteLine($"[SMTC] Status != Playing ({playback.PlaybackStatus}), ����������");
                return;
            }

            var mediaProps = await session.ControlSession.TryGetMediaPropertiesAsync();

            // ��������� ��� ���� ��� ������
            var title = mediaProps.Title ?? "";
            var artist = mediaProps.Artist ?? "";

            // ���� ������ ��� ������ - ����������
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(artist))
            {
                Console.WriteLine($"[SMTC] ��� title/artist, ����������");
                return;
            }

            // ��������� ������ ����
            if (string.IsNullOrEmpty(title)) title = "Unknown";
            if (string.IsNullOrEmpty(artist)) artist = "Unknown";

            // Get thumbnail
            byte[]? thumbnailData = null;
            if (mediaProps.Thumbnail != null)
            {
                try
                {
                    using var stream = await mediaProps.Thumbnail.OpenReadAsync();
                    using var reader = new DataReader(stream);
                    await reader.LoadAsync((uint)stream.Size);
                    thumbnailData = new byte[stream.Size];
                    reader.ReadBytes(thumbnailData);
                }
                catch { }
            }

            // ���� ��� thumbnail - retry �� 1.5 ������
            if (thumbnailData == null && retryCount < MAX_RETRIES)
            {
                Console.WriteLine($"[SMTC] ��� thumbnail ��� {artist} - {title}, retry {retryCount + 1}/{MAX_RETRIES}...");
                await Task.Delay(RETRY_DELAY_MS);
                await SendMediaUpdate(session, retryCount + 1);
                return;
            }

            // ����� retry ���������� � ���������
            if (thumbnailData == null)
            {
                Console.WriteLine($"[SMTC] ��� thumbnail ����� {MAX_RETRIES} �������, ���������� � ���������");
            }

            var fingerprint = $"{session.Id}||{title}||{artist}";
            var isNewTrack = fingerprint != _lastFingerprint;

            // Get timeline info (position, duration)
            var timelineProps = session.ControlSession.GetTimelineProperties();
            var playbackInfo = session.ControlSession.GetPlaybackInfo();
            var currentPosition = timelineProps.Position.TotalSeconds;
            var duration = timelineProps.EndTime.TotalSeconds;

            // ��������� ��� �������
            _knownPosition = currentPosition;
            _knownPositionTime = DateTime.Now;
            _knownDuration = duration;

            // Reset position to 0 for new track
            if (isNewTrack)
            {
                _lastSentPosition = 0;
            }

            // Check if position changed significantly (> 2 seconds = seek)
            var positionDiff = Math.Abs(currentPosition - _lastSentPosition);
            var positionChanged = positionDiff > POSITION_THRESHOLD;

            // Debounce: skip if same fingerprint, position hasn't changed much, and too soon
            var now = DateTime.Now;
            if (!isNewTrack && !positionChanged && (now - _lastSentTime).TotalMilliseconds < DEBOUNCE_MS)
            {
                return;
            }

            var data = new
            {
                media = new
                {
                    title,
                    artist,
                    thumbnail = thumbnailData != null ? new { data = thumbnailData } : null
                },
                timeline = new
                {
                    position = currentPosition,
                    duration = duration
                },
                playback = new
                {
                    playbackStatus = (int)playbackInfo.PlaybackStatus
                }
            };

            var json = JsonSerializer.Serialize(data);

            // Skip if exact same JSON (unless position changed significantly or new track)
            if (json == _lastSentJson && !positionChanged && !isNewTrack)
            {
                return;
            }

            var isSeek = positionChanged && !isNewTrack;

            _lastFingerprint = fingerprint;
            _lastSentJson = json;
            _lastSentTime = now;
            _lastSentPosition = currentPosition;
            BroadcastMessage(json);

            if (isNewTrack)
            {
                Console.WriteLine($"[SMTC] NEW: {artist} - {title} (duration: {duration:F0}s)");

                // SMTC often sends duration=0 on first event, resend after 1 second to get correct duration
                if (duration <= 0)
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(1000);
                        // Check if still the same track
                        if (_lastFingerprint == fingerprint)
                        {
                            Console.WriteLine($"[SMTC] Resending for duration update...");
                            await SendMediaUpdate(session);
                        }
                    });
                }
            }
            else if (isSeek)
            {
                Console.WriteLine($"[SMTC] {artist} - {title} (seek to {currentPosition:F0}s)");
            }
            else
            {
                Console.WriteLine($"[SMTC] {artist} - {title}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SMTC] Error: {ex.Message}");
        }
    }

    static void SendPlaybackUpdate(MediaManager.MediaSession session)
    {
        try
        {
            var playback = session.ControlSession.GetPlaybackInfo();
            var timelineProps = session.ControlSession.GetTimelineProperties();
            var currentPosition = timelineProps.Position.TotalSeconds;

            // ��������� ���� (����� � ��������� ������� �������)
            _knownPosition = currentPosition;
            _knownPositionTime = DateTime.Now;

            var data = new
            {
                timeline = new
                {
                    position = currentPosition,
                    duration = timelineProps.EndTime.TotalSeconds
                },
                playback = new
                {
                    playbackStatus = (int)playback.PlaybackStatus
                }
            };

            var json = JsonSerializer.Serialize(data);
            _lastSentPosition = currentPosition;
            BroadcastMessage(json);
            Console.WriteLine($"[SMTC] Playback status: {playback.PlaybackStatus}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SMTC] Playback update error: {ex.Message}");
        }
    }

    static void SendTimelineUpdate(MediaManager.MediaSession session)
    {
        try
        {
            var playback = session.ControlSession.GetPlaybackInfo();
            var timelineProps = session.ControlSession.GetTimelineProperties();
            var currentPosition = timelineProps.Position.TotalSeconds;
            var newDuration = timelineProps.EndTime.TotalSeconds;

            // ������ ��������� duration (�������� ��� ����� �����)
            var durationChanged = Math.Abs(newDuration - _knownDuration) > 0.5;
            _knownDuration = newDuration;

            // Check if position changed significantly (> 2 seconds = seek)
            var positionDiff = Math.Abs(currentPosition - _lastSentPosition);
            if (positionDiff <= POSITION_THRESHOLD && !durationChanged)
            {
                return;
            }

            // ��������� ���� ��� ������� (seek ��� ����� ������������)
            _knownPosition = currentPosition;
            _knownPositionTime = DateTime.Now;

            var data = new
            {
                timeline = new
                {
                    position = currentPosition,
                    duration = newDuration
                },
                playback = new
                {
                    playbackStatus = (int)playback.PlaybackStatus
                }
            };

            var json = JsonSerializer.Serialize(data);
            _lastSentPosition = currentPosition;
            BroadcastMessage(json);
            Console.WriteLine($"[SMTC] Timeline: {currentPosition:F0}s / {newDuration:F0}s{(durationChanged ? " (duration changed)" : " (seek)")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SMTC] Timeline update error: {ex.Message}");
        }
    }

    // ��������� ������ �������� ������� ������ �������
    static void StartPositionTimer()
    {
        StopPositionTimer();
        _positionTimer = new System.Threading.Timer(_ =>
        {
            try
            {
                if (_activeSessionId == null || _mediaManager == null) return;
                if (_knownDuration <= 0) return;

                // ������� ������� ����: basePosition + elapsed
                var elapsed = (DateTime.Now - _knownPositionTime).TotalSeconds;
                var currentPosition = Math.Min(_knownPosition + elapsed, _knownDuration);

                var data = new
                {
                    timeline = new
                    {
                        position = Math.Floor(currentPosition),
                        duration = _knownDuration
                    },
                    playback = new
                    {
                        playbackStatus = 4 // Playing
                    }
                };

                var json = JsonSerializer.Serialize(data);
                _lastSentPosition = currentPosition;
                BroadcastMessage(json);
            }
            catch { }
        }, null, 1000, 1000);
    }

    // ������������� ������ �������
    static void StopPositionTimer()
    {
        _positionTimer?.Dispose();
        _positionTimer = null;
    }

    static void BroadcastMessage(string message)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        var segment = new ArraySegment<byte>(buffer);

        List<WebSocket> snapshot;
        lock (_lock)
        {
            snapshot = new List<WebSocket>(_clients);
        }

        var deadClients = new List<WebSocket>();
        foreach (var client in snapshot)
        {
            try
            {
                if (client.State == WebSocketState.Open)
                {
                    using var cts = new CancellationTokenSource(2000);
                    // Fire-and-forget async send � don't block the thread pool
                    var task = client.SendAsync(segment, WebSocketMessageType.Text, true, cts.Token);
                    if (!task.Wait(2000))
                    {
                        // Send timed out � treat as dead
                        deadClients.Add(client);
                    }
                }
                else
                {
                    deadClients.Add(client);
                }
            }
            catch
            {
                deadClients.Add(client);
            }
        }

        if (deadClients.Count > 0)
        {
            lock (_lock)
            {
                foreach (var dead in deadClients)
                {
                    _clients.Remove(dead);
                    try { dead.Abort(); } catch { }
                    try { dead.Dispose(); } catch { }
                }
            }
        }
    }

    // TCP proxy: ������� �� 0.0.0.0 (��� ����������, ��� ������) � ��������� �� localhost HttpListener
    static async Task StartTcpProxy(int publicPort, int internalPort, string label)
    {
        var listener = new TcpListener(IPAddress.Any, publicPort);
        listener.Start();
        Console.WriteLine($"[{label}] Proxy 0.0.0.0:{publicPort} -> localhost:{internalPort}");

        while (true)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync();
                _ = ForwardConnection(client, internalPort);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{label}] Proxy error: {ex.Message}");
            }
        }
    }

    static async Task ForwardConnection(TcpClient client, int localPort)
    {
        using var cts = new CancellationTokenSource();
        try
        {
            using (client)
            {
                var cs = client.GetStream();

                // Read HTTP headers
                var buf = new byte[8192];
                var ms = new MemoryStream();
                int headerEnd = -1;

                while (headerEnd < 0)
                {
                    int n = await cs.ReadAsync(buf, 0, buf.Length);
                    if (n == 0) return;
                    int prevLen = (int)ms.Length;
                    ms.Write(buf, 0, n);
                    var data = ms.ToArray();
                    for (int i = Math.Max(0, prevLen - 3); i <= data.Length - 4; i++)
                    {
                        if (data[i] == '\r' && data[i + 1] == '\n' && data[i + 2] == '\r' && data[i + 3] == '\n')
                        {
                            headerEnd = i + 4;
                            break;
                        }
                    }
                }

                var allData = ms.ToArray();
                var headers = Encoding.ASCII.GetString(allData, 0, headerEnd);

                // Parse request line (e.g. "GET /auth-callback?token=xxx HTTP/1.1")
                var firstLine = headers.Substring(0, headers.IndexOf("\r\n"));
                var parts = firstLine.Split(' ');
                var requestPath = parts.Length > 1 ? parts[1] : "";

                // /auth-callback is now a frontend SvelteKit page, forward normally

                // All other requests: rewrite Host and forward to HttpListener
                using (var upstream = new TcpClient())
                {
                    await upstream.ConnectAsync(IPAddress.Loopback, localPort);
                    var us = upstream.GetStream();

                    var hostLine = $"Host: localhost:{localPort}";
                    headers = System.Text.RegularExpressions.Regex.Replace(
                        headers, @"(?im)^Host:[^\r\n]+", hostLine);

                    var newHeaders = Encoding.ASCII.GetBytes(headers);
                    await us.WriteAsync(newHeaders, 0, newHeaders.Length);

                    if (allData.Length > headerEnd)
                        await us.WriteAsync(allData, headerEnd, allData.Length - headerEnd);

                    var t1 = cs.CopyToAsync(us, cts.Token);
                    var t2 = us.CopyToAsync(cs, cts.Token);
                    await Task.WhenAny(t1, t2);
                    // One direction closed � cancel the other to prevent hanging tasks
                    cts.Cancel();
                }
            }
        }
        catch { }
    }

    static async Task HandleAuthCallbackDirect(NetworkStream cs, string requestPath)
    {
        // Parse query string from path
        string? token = null;
        string? nickname = null;
        var qIdx = requestPath.IndexOf('?');
        if (qIdx >= 0)
        {
            var qs = requestPath.Substring(qIdx + 1);
            foreach (var pair in qs.Split('&'))
            {
                var kv = pair.Split('=', 2);
                if (kv.Length == 2)
                {
                    var key = Uri.UnescapeDataString(kv[0]);
                    var val = Uri.UnescapeDataString(kv[1]);
                    if (key == "token") token = val;
                    if (key == "nickname") nickname = val;
                }
            }
        }

        string body;
        if (string.IsNullOrEmpty(token))
        {
            body = "<html><body><h1>Error: no token</h1></body></html>";
        }
        else
        {
            nickname ??= DecodeJwtNickname(token);
            _siteAuthToken = token;
            _siteAuthNickname = nickname;
            SaveSiteAuth();
            Console.WriteLine($"[API] Auth callback - logged in as: {nickname}");

            body = @"<html>
<head><style>
  body { background: #0a0a0a; color: #fff; font-family: monospace; display: flex; align-items: center; justify-content: center; height: 100vh; margin: 0; }
  .box { text-align: center; }
  h1 { font-size: 2rem; margin-bottom: 0.5rem; }
  p { color: rgba(255,255,255,0.6); }
</style></head>
<body><div class='box'>
  <h1>Logged in</h1>
  <p>You can close this window</p>
  <script>setTimeout(()=>window.close(),2000);</script>
</div></body></html>";
        }

        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var status = string.IsNullOrEmpty(token) ? "400 Bad Request" : "200 OK";
        var response = $"HTTP/1.1 {status}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\nAccess-Control-Allow-Origin: *\r\n\r\n";
        var responseBytes = Encoding.ASCII.GetBytes(response);

        await cs.WriteAsync(responseBytes, 0, responseBytes.Length);
        await cs.WriteAsync(bodyBytes, 0, bodyBytes.Length);
    }

    static async Task StartWebSocketServer()
    {
        int internalPort = WS_PORT + 1;
        _wsListener = new HttpListener();
        _wsListener.Prefixes.Add($"http://localhost:{internalPort}/");
        _wsListener.Start();

        // TCP proxy �� ��������� �����
        _ = Task.Run(() => StartTcpProxy(WS_PORT, internalPort, "WS"));
        Console.WriteLine($"[WS] WebSocket ������ �� 0.0.0.0:{WS_PORT}");

        while (true)
        {
            try
            {
                var context = await _wsListener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    _ = HandleWebSocketClient(context);
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WS] Error: {ex.Message}");
            }
        }
    }

    static async Task SendCurrentStateToClient(WebSocket ws, int retryCount = 0)
    {
        const int RETRY_DELAY_MS = 300;
        const int MAX_RETRIES = 2;

        if (_mediaManager == null) return;

        // ���� �������� ������ (� ������ �������)
        foreach (var session in _mediaManager.CurrentMediaSessions.Values)
        {
            try
            {
                var playback = session.ControlSession.GetPlaybackInfo();
                if (playback.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    continue;

                var sourceId = session.Id ?? "";
                AddSeenSource(sourceId);
                if (!ShouldAllowSource(sourceId))
                    continue;

                var mediaProps = await session.ControlSession.TryGetMediaPropertiesAsync();
                var title = mediaProps.Title ?? "";
                var artist = mediaProps.Artist ?? "";

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(artist))
                    continue;

                // Get thumbnail
                byte[]? thumbnailData = null;
                if (mediaProps.Thumbnail != null)
                {
                    try
                    {
                        using var stream = await mediaProps.Thumbnail.OpenReadAsync();
                        using var reader = new DataReader(stream);
                        await reader.LoadAsync((uint)stream.Size);
                        thumbnailData = new byte[stream.Size];
                        reader.ReadBytes(thumbnailData);
                    }
                    catch { }
                }

                // ���� ��� thumbnail - retry �� MAX_RETRIES ���
                if (thumbnailData == null && retryCount < MAX_RETRIES)
                {
                    if (ws.State == WebSocketState.Open)
                    {
                        Console.WriteLine($"[WS] ��� thumbnail ��� ������ �������, retry {retryCount + 1}/{MAX_RETRIES}...");
                        await Task.Delay(RETRY_DELAY_MS);
                        await SendCurrentStateToClient(ws, retryCount + 1);
                    }
                    return;
                }

                // ����� MAX_RETRIES ���������� ��� ��������
                if (thumbnailData == null)
                {
                    Console.WriteLine($"[WS] ��� thumbnail ����� {MAX_RETRIES} �������, ���������� ��� ��������");
                }

                // Get timeline info
                var timelineProps = session.ControlSession.GetTimelineProperties();

                var data = new
                {
                    media = new
                    {
                        title,
                        artist,
                        thumbnail = thumbnailData != null ? new { data = thumbnailData } : null
                    },
                    timeline = new
                    {
                        position = timelineProps.Position.TotalSeconds,
                        duration = timelineProps.EndTime.TotalSeconds
                    },
                    playback = new
                    {
                        playbackStatus = (int)playback.PlaybackStatus
                    }
                };

                var json = JsonSerializer.Serialize(data);
                var buffer = Encoding.UTF8.GetBytes(json);
                var segment = new ArraySegment<byte>(buffer);

                if (ws.State == WebSocketState.Open)
                {
                    await ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                    Console.WriteLine($"[WS] ���������� ������� ���������: {artist} - {title} (pos: {timelineProps.Position.TotalSeconds:F0}s)");
                }
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WS] ������ ��������� ���������: {ex.Message}");
            }
        }

        // ��� �������� ������ - retry �� MAX_RETRIES ���
        if (ws.State == WebSocketState.Open && retryCount < MAX_RETRIES)
        {
            Console.WriteLine($"[WS] ��� �������� ������ ��� ������ �������, retry {retryCount + 1}/{MAX_RETRIES}...");
            await Task.Delay(RETRY_DELAY_MS);
            await SendCurrentStateToClient(ws, retryCount + 1);
        }
        else if (retryCount >= MAX_RETRIES)
        {
            Console.WriteLine($"[WS] ��� �������� ������ ����� {MAX_RETRIES} �������");
        }
    }

    static async Task HandleWebSocketClient(HttpListenerContext context)
    {
        var wsContext = await context.AcceptWebSocketAsync(null);
        var ws = wsContext.WebSocket;
        var lastPong = DateTime.Now;

        lock (_lock)
        {
            _clients.Add(ws);
        }
        Console.WriteLine("[WS] ������ �����������");

        // ���������� ������� ��������� ������ �������
        await SendCurrentStateToClient(ws);

        var buffer = new byte[1024];
        var receiveBuffer = new byte[1024];
        
        // Start ping timer (every 30 seconds)
        var pingTimer = new System.Threading.Timer(async _ =>
        {
            try
            {
                if (ws.State == WebSocketState.Open)
                {
                    // Check if client responded to last ping
                    if ((DateTime.Now - lastPong).TotalSeconds > 90)
                    {
                        Console.WriteLine("[WS] Client timed out (no pong), removing...");
                        lock (_lock) { _clients.Remove(ws); }
                        try { ws.Abort(); } catch { }
                        try { ws.Dispose(); } catch { }
                        return;
                    }
                    
                    // Send ping
                    var pingData = Encoding.UTF8.GetBytes("ping");
                    await ws.SendAsync(new ArraySegment<byte>(pingData), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            catch { }
        }, null, 30000, 30000);

        try
        {
            while (ws.State == WebSocketState.Open)
            {
                // Timeout on receive � detect dead connections that didn't send close frame
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), cts.Token);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    break;
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    // Check for pong response (client sends "pong")
                    var receivedText = Encoding.UTF8.GetString(receiveBuffer, 0, result.Count);
                    if (receivedText.Trim().ToLower() == "pong")
                    {
                        lastPong = DateTime.Now;
                    }
                }
            }
        }
        catch { }
        finally
        {
            pingTimer.Dispose();
            lock (_lock)
            {
                _clients.Remove(ws);
            }
            try { ws.Abort(); } catch { }
            try { ws.Dispose(); } catch { }
            Console.WriteLine("[WS] ������ ����������");
        }
    }

    static async Task StartHttpServer()
    {
        _httpListener = new HttpListener();

        // Try binding directly to all interfaces (no proxy needed)
        try
        {
            _httpListener.Prefixes.Add($"http://+:{HTTP_PORT}/");
            _httpListener.Start();
            Console.WriteLine($"[HTTP] ������ bind �� 0.0.0.0:{HTTP_PORT}");
        }
        catch
        {
            // No permission for +, try registering urlacl then retry
            _httpListener.Close();
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"http add urlacl url=http://+:{HTTP_PORT}/ user={Environment.UserDomainName}\\{Environment.UserName}",
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };
                var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit(5000);

                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"http://+:{HTTP_PORT}/");
                _httpListener.Start();
                Console.WriteLine($"[HTTP] ������ bind �� 0.0.0.0:{HTTP_PORT} (����� urlacl)");
            }
            catch
            {
                // Fallback: localhost + TCP proxy
                _httpListener = new HttpListener();
                int internalPort = HTTP_PORT + 1;
                _httpListener.Prefixes.Add($"http://localhost:{internalPort}/");
                _httpListener.Start();
                _ = Task.Run(() => StartTcpProxy(HTTP_PORT, internalPort, "HTTP"));
                Console.WriteLine($"[HTTP] Fallback: proxy 0.0.0.0:{HTTP_PORT} -> localhost:{internalPort}");
            }
        }

        Console.WriteLine($"[HTTP] ������ �� 0.0.0.0:{HTTP_PORT}");

        string? staticDir = null;

        if (NO_FRONTEND)
        {
            Console.WriteLine("[HTTP] NO_FRONTEND ������� � ������� �� �������, ������ API");
        }
        else
        {
            // Find frontBuild directory
            var possiblePaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "frontBuild"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "frontBuild"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "frontBuild"),
                Path.Combine(AppContext.BaseDirectory, "frontBuild"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "frontBuild"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "frontBuild"),
                Path.Combine(Directory.GetCurrentDirectory(), "frontBuild"),
                @"C:\Users\000-d\Desktop\JShit\Unik player reps\unikPlayer\frontBuild"
            };

            foreach (var p in possiblePaths)
            {
                var full = Path.GetFullPath(p);
                if (File.Exists(Path.Combine(full, "index.html")))
                {
                    staticDir = full;
                    break;
                }
            }

            if (staticDir == null)
            {
                Console.WriteLine("[HTTP] ������: frontBuild �� ������!");
                return;
            }

            Console.WriteLine($"[HTTP] Serving: {staticDir}");
        }

        while (true)
        {
            try
            {
                var context = await _httpListener.GetContextAsync();
                _ = HandleHttpRequest(context, staticDir);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HTTP] Error: {ex.Message}");
            }
        }
    }

    static async Task HandleHttpRequest(HttpListenerContext context, string? staticDir)
    {
        var request = context.Request;
        var response = context.Response;
        var path = request.Url?.AbsolutePath ?? "/";

        // CORS headers for dev mode
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        // Handle preflight
        if (request.HttpMethod == "OPTIONS")
        {
            response.StatusCode = 204;
            response.Close();
            return;
        }

        // API: GET /api/fonts - get system fonts
        if (path == "/api/fonts" && request.HttpMethod == "GET")
        {
            await HandleGetFonts(response);
            return;
        }

        // API: GET /api/styles - load styles
        if (path == "/api/styles" && request.HttpMethod == "GET")
        {
            await HandleGetStyles(response);
            return;
        }

        // API: POST /api/styles - save styles
        if (path == "/api/styles" && request.HttpMethod == "POST")
        {
            await HandlePostStyles(request, response);
            return;
        }

        // API: GET /api/open-styles-folder - open folder in explorer
        if (path == "/api/open-styles-folder" && request.HttpMethod == "GET")
        {
            await HandleOpenStylesFolder(response);
            return;
        }

        // API: GET /api/css/{playerName} - get CSS for player
        if (path.StartsWith("/api/css/") && request.HttpMethod == "GET")
        {
            var playerName = path.Substring("/api/css/".Length);
            await HandleGetCSS(playerName, response);
            return;
        }

        // API: POST /api/css/{playerName} - save CSS for player
        if (path.StartsWith("/api/css/") && request.HttpMethod == "POST")
        {
            var playerName = path.Substring("/api/css/".Length);
            await HandlePostCSS(playerName, request, response);
            return;
        }

        // API: DELETE /api/css/{playerName} - reset CSS (delete user customization)
        if (path.StartsWith("/api/css/") && request.HttpMethod == "DELETE")
        {
            var playerName = path.Substring("/api/css/".Length);
            await HandleDeleteCSS(playerName, response);
            return;
        }

        // API: GET /api/open-css/{playerName} - open CSS file in explorer
        if (path.StartsWith("/api/open-css/") && request.HttpMethod == "GET")
        {
            var playerName = path.Substring("/api/open-css/".Length);
            await HandleOpenCSS(playerName, response);
            return;
        }

        // API: GET /api/open-html/{playerName} - open custom player HTML file in default editor
        if (path.StartsWith("/api/open-html/") && request.HttpMethod == "GET")
        {
            var playerName = path.Substring("/api/open-html/".Length);
            await HandleOpenHTML(playerName, response);
            return;
        }

        // ========================================
        // MEDIA FILTER API
        // ========================================

        // API: GET /api/media-filter
        if (path == "/api/media-filter" && request.HttpMethod == "GET")
        {
            await HandleGetMediaFilter(response);
            return;
        }

        // API: POST /api/media-filter
        if (path == "/api/media-filter" && request.HttpMethod == "POST")
        {
            await HandlePostMediaFilter(request, response);
            return;
        }

        // ========================================
        // CUSTOM PLAYERS API
        // ========================================

        // API: GET /api/custom-players - list all custom players
        if (path == "/api/custom-players" && request.HttpMethod == "GET")
        {
            await HandleGetCustomPlayers(response);
            return;
        }

        // API: POST /api/custom-players - upload new custom player
        if (path == "/api/custom-players" && request.HttpMethod == "POST")
        {
            await HandlePostCustomPlayer(request, response);
            return;
        }

        // API: POST /api/custom-players/validate - validate HTML
        if (path == "/api/custom-players/validate" && request.HttpMethod == "POST")
        {
            await HandleValidateCustomPlayer(request, response);
            return;
        }

        // API: GET /api/custom-players/{name}/backup - get backup
        if (path.StartsWith("/api/custom-players/") && path.EndsWith("/backup") && request.HttpMethod == "GET")
        {
            var name = path.Replace("/api/custom-players/", "").Replace("/backup", "");
            await HandleGetCustomPlayerBackup(name, response);
            return;
        }

        // API: POST /api/custom-players/{name}/reset - reset to backup
        if (path.StartsWith("/api/custom-players/") && path.EndsWith("/reset") && request.HttpMethod == "POST")
        {
            var name = path.Replace("/api/custom-players/", "").Replace("/reset", "");
            await HandleResetCustomPlayer(name, response);
            return;
        }

        // API: GET /api/custom-players/{name} - get player HTML
        if (path.StartsWith("/api/custom-players/") && request.HttpMethod == "GET")
        {
            var name = path.Substring("/api/custom-players/".Length);
            await HandleGetCustomPlayer(name, response);
            return;
        }

        // API: PUT /api/custom-players/{name} - update player HTML
        if (path.StartsWith("/api/custom-players/") && request.HttpMethod == "PUT")
        {
            var name = path.Substring("/api/custom-players/".Length);
            await HandleUpdateCustomPlayer(name, request, response);
            return;
        }

        // API: DELETE /api/custom-players/{name} - delete player
        if (path.StartsWith("/api/custom-players/") && request.HttpMethod == "DELETE")
        {
            var name = path.Substring("/api/custom-players/".Length);
            await HandleDeleteCustomPlayer(name, response);
            return;
        }

        // ========================================
        // SITE AUTH API (Cloud Sync)
        // ========================================

        // API: GET /api/site-auth - get stored auth state
        if (path == "/api/site-auth" && request.HttpMethod == "GET")
        {
            await HandleGetSiteAuth(response);
            return;
        }

        // API: POST /api/site-auth - save token from frontend auth-callback page
        if (path == "/api/site-auth" && request.HttpMethod == "POST")
        {
            await HandlePostSiteAuth(request, response);
            return;
        }

        // API: DELETE /api/site-auth - logout (clear auth state)
        if (path == "/api/site-auth" && request.HttpMethod == "DELETE")
        {
            await HandleDeleteSiteAuth(response);
            return;
        }

        // ========================================
        // FRONTEND LOGS API
        // ========================================

        // API: POST /api/frontend-logs - receive frontend logs
        if (path == "/api/frontend-logs" && request.HttpMethod == "POST")
        {
            await HandleFrontendLogs(request, response);
            return;
        }

        // API: GET /api/frontend-logs - get stored frontend logs
        if (path == "/api/frontend-logs" && request.HttpMethod == "GET")
        {
            await HandleGetFrontendLogs(response);
            return;
        }

        // API: DELETE /api/frontend-logs - clear frontend logs
        if (path == "/api/frontend-logs" && request.HttpMethod == "DELETE")
        {
            await HandleClearFrontendLogs(response);
            return;
        }

        // Static file serving
        if (staticDir == null)
        {
            response.StatusCode = 404;
            response.Close();
            return;
        }

        if (path == "/") path = "/index.html";

        var filePath = Path.GetFullPath(Path.Combine(staticDir, path.TrimStart('/')));

        // Security check
        if (!filePath.StartsWith(staticDir, StringComparison.OrdinalIgnoreCase))
        {
            response.StatusCode = 403;
            response.Close();
            return;
        }

        // Try path/index.html for directory-style routes (e.g. /auth-callback -> auth-callback/index.html)
        if (!File.Exists(filePath) && !Path.HasExtension(filePath))
        {
            var dirIndex = Path.Combine(filePath, "index.html");
            if (File.Exists(dirIndex))
            {
                filePath = dirIndex;
            }
        }

        // If file not found, serve index.html (SPA routing)
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"[HTTP] File not found: {filePath} -> fallback to index.html");
            filePath = Path.Combine(staticDir, "index.html");
        }

        try
        {
            var content = await File.ReadAllBytesAsync(filePath);
            var ext = Path.GetExtension(filePath).ToLower();
            response.ContentType = GetMimeType(ext);
            response.ContentLength64 = content.Length;
            await response.OutputStream.WriteAsync(content);
        }
        catch
        {
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    static async Task HandleGetStyles(HttpListenerResponse response)
    {
        try
        {
            string json = "{}";
            if (File.Exists(STYLES_FILE))
            {
                json = await File.ReadAllTextAsync(STYLES_FILE);
            }

            var content = Encoding.UTF8.GetBytes(json);
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = content.Length;
            await response.OutputStream.WriteAsync(content);
            Console.WriteLine("[API] GET /api/styles");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] GET /api/styles error: {ex.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    static async Task HandlePostStyles(HttpListenerRequest request, HttpListenerResponse response)
    {
        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = await reader.ReadToEndAsync();

            // Validate JSON
            JsonDocument.Parse(body);

            // Ensure directory exists
            Directory.CreateDirectory(STYLES_DIR);

            // Save to file
            await File.WriteAllTextAsync(STYLES_FILE, body);

            var result = Encoding.UTF8.GetBytes("{\"success\":true}");
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = result.Length;
            await response.OutputStream.WriteAsync(result);
            Console.WriteLine($"[API] POST /api/styles saved to {STYLES_FILE}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] POST /api/styles error: {ex.Message}");
            var result = Encoding.UTF8.GetBytes("{\"success\":false}");
            response.StatusCode = 400;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = result.Length;
            await response.OutputStream.WriteAsync(result);
        }
        finally
        {
            response.Close();
        }
    }

    static async Task HandleOpenStylesFolder(HttpListenerResponse response)
    {
        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(STYLES_DIR);

            // Open folder in Explorer and select the file if it exists
            if (File.Exists(STYLES_FILE))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{STYLES_FILE}\"");
            }
            else
            {
                System.Diagnostics.Process.Start("explorer.exe", STYLES_DIR);
            }

            var result = Encoding.UTF8.GetBytes("{\"success\":true}");
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = result.Length;
            await response.OutputStream.WriteAsync(result);
            Console.WriteLine($"[API] Opened styles folder: {STYLES_DIR}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] open-styles-folder error: {ex.Message}");
            response.StatusCode = 500;
            var result = Encoding.UTF8.GetBytes("{\"success\":false}");
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = result.Length;
            await response.OutputStream.WriteAsync(result);
        }
        finally
        {
            response.Close();
        }
    }

    static async Task HandleGetFonts(HttpListenerResponse response)
    {
        try
        {
            using var fonts = new InstalledFontCollection();
            var fontList = fonts.Families
                .Where(f => !string.IsNullOrEmpty(f.Name) && !f.Name.StartsWith("@"))
                .Select(f => f.Name)
                .OrderBy(f => f)
                .ToList();

            var json = JsonSerializer.Serialize(new { fonts = fontList });
            var content = Encoding.UTF8.GetBytes(json);
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = content.Length;
            await response.OutputStream.WriteAsync(content);
            Console.WriteLine($"[API] GET /api/fonts - {fontList.Count} fonts");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] GET /api/fonts error: {ex.Message}");
            response.StatusCode = 500;
            var result = Encoding.UTF8.GetBytes("{\"fonts\":[]}");
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = result.Length;
            await response.OutputStream.WriteAsync(result);
        }
        finally
        {
            response.Close();
        }
    }

    static async Task HandleGetCSS(string playerName, HttpListenerResponse response)
    {
        try
        {
            // Sanitize player name
            playerName = Path.GetFileNameWithoutExtension(playerName);
            var cssFile = Path.Combine(CSS_DIR, $"{playerName}.css");

            string css = "";
            if (File.Exists(cssFile))
            {
                css = await File.ReadAllTextAsync(cssFile);
            }

            var content = Encoding.UTF8.GetBytes(css);
            response.ContentType = "text/css; charset=utf-8";
            response.ContentLength64 = content.Length;
            await response.OutputStream.WriteAsync(content);
            Console.WriteLine($"[API] GET /api/css/{playerName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] GET /api/css error: {ex.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    static async Task HandlePostCSS(string playerName, HttpListenerRequest request, HttpListenerResponse response)
    {
        try
        {
            // Sanitize player name
            playerName = Path.GetFileNameWithoutExtension(playerName);
            var cssFile = Path.Combine(CSS_DIR, $"{playerName}.css");

            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var css = await reader.ReadToEndAsync();

            // Ensure directory exists
            Directory.CreateDirectory(CSS_DIR);

            // Save CSS file
            await File.WriteAllTextAsync(cssFile, css);

            var result = Encoding.UTF8.GetBytes("{\"success\":true}");
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = result.Length;
            await response.OutputStream.WriteAsync(result);
            Console.WriteLine($"[API] POST /api/css/{playerName} saved to {cssFile}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] POST /api/css error: {ex.Message}");
            response.StatusCode = 500;
            var result = Encoding.UTF8.GetBytes("{\"success\":false}");
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = result.Length;
            await response.OutputStream.WriteAsync(result);
        }
        finally
        {
            response.Close();
        }
    }

    static async Task HandleDeleteCSS(string playerName, HttpListenerResponse response)
    {
        try
        {
            playerName = Path.GetFileNameWithoutExtension(playerName);
            var cssFile = Path.Combine(CSS_DIR, $"{playerName}.css");

            if (File.Exists(cssFile))
            {
                File.Delete(cssFile);
                Console.WriteLine($"[API] DELETE /api/css/{playerName} - file deleted");
            }
            else
            {
                Console.WriteLine($"[API] DELETE /api/css/{playerName} - no file to delete");
            }

            var result = Encoding.UTF8.GetBytes("{\"success\":true}");
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = result.Length;
            await response.OutputStream.WriteAsync(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] DELETE /api/css error: {ex.Message}");
            response.StatusCode = 500;
            var result = Encoding.UTF8.GetBytes("{\"success\":false}");
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = result.Length;
            await response.OutputStream.WriteAsync(result);
        }
        finally
        {
            response.Close();
        }
    }

    static async Task HandleOpenHTML(string playerName, HttpListenerResponse response)
    {
        try
        {
            playerName = new string(playerName.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
            var htmlFile = Path.Combine(CUSTOM_PLAYERS_DIR, $"{playerName}.html");

            if (!File.Exists(htmlFile))
            {
                response.StatusCode = 404;
                var err = Encoding.UTF8.GetBytes("{\"error\":\"File not found\"}");
                response.ContentType = "application/json; charset=utf-8";
                response.ContentLength64 = err.Length;
                await response.OutputStream.WriteAsync(err);
                return;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "rundll32.exe",
                Arguments = $"shell32.dll,OpenAs_RunDLL {htmlFile}",
                UseShellExecute = false
            });

            var result = Encoding.UTF8.GetBytes("{\"success\":true}");
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = result.Length;
            await response.OutputStream.WriteAsync(result);
            Console.WriteLine($"[API] Opened HTML file: {htmlFile}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] open-html error: {ex.Message}");
            response.StatusCode = 500;
            var result = Encoding.UTF8.GetBytes("{\"success\":false}");
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = result.Length;
            await response.OutputStream.WriteAsync(result);
        }
        finally { response.OutputStream.Close(); }
    }

    static async Task HandleOpenCSS(string playerName, HttpListenerResponse response)
    {
        try
        {
            // Sanitize player name
            playerName = Path.GetFileNameWithoutExtension(playerName);
            var cssFile = Path.Combine(CSS_DIR, $"{playerName}.css");

            // Ensure directory exists
            Directory.CreateDirectory(CSS_DIR);

            // Create empty file if it doesn't exist
            if (!File.Exists(cssFile))
            {
                await File.WriteAllTextAsync(cssFile, "/* Custom styles for " + playerName + " */\n");
            }

            // Open file in default editor
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = cssFile,
                UseShellExecute = true
            });

            var result = Encoding.UTF8.GetBytes("{\"success\":true}");
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = result.Length;
            await response.OutputStream.WriteAsync(result);
            Console.WriteLine($"[API] Opened CSS file: {cssFile}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] open-css error: {ex.Message}");
            response.StatusCode = 500;
            var result = Encoding.UTF8.GetBytes("{\"success\":false}");
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = result.Length;
            await response.OutputStream.WriteAsync(result);
        }
        finally
        {
            response.Close();
        }
    }

    // ========================================
    // MEDIA FILTER HANDLERS
    // ========================================

    static async Task HandleGetMediaFilter(HttpListenerResponse response)
    {
        try
        {
            // Build source info with display names and current media
            var sourceInfoList = new List<object>();

            lock (_filterLock)
            {
                foreach (var sourceId in _mediaFilter.seenSources)
                {
                    var displayName = GetAppDisplayName(sourceId);
                    string? mediaTitle = null;
                    string? mediaArtist = null;
                    bool isPlaying = false;

                    // Try to get current media info for this source
                    if (_mediaManager != null)
                    {
                        foreach (var session in _mediaManager.CurrentMediaSessions.Values)
                        {
                            if (session.Id == sourceId)
                            {
                                try
                                {
                                    var playback = session.ControlSession.GetPlaybackInfo();
                                    isPlaying = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                                    // Get media properties synchronously (we're in a lock)
                                    var propsTask = session.ControlSession.TryGetMediaPropertiesAsync().AsTask();
                                    if (propsTask.Wait(500)) // 500ms timeout
                                    {
                                        var props = propsTask.Result;
                                        mediaTitle = props.Title;
                                        mediaArtist = props.Artist;
                                    }
                                }
                                catch { }
                                break;
                            }
                        }
                    }

                    sourceInfoList.Add(new
                    {
                        id = sourceId,
                        displayName,
                        title = mediaTitle,
                        artist = mediaArtist,
                        isPlaying
                    });
                }
            }

            var result = new
            {
                mode = _mediaFilter.mode,
                sources = _mediaFilter.sources,
                seenSources = _mediaFilter.seenSources,
                sourceInfo = sourceInfoList
            };

            var json = JsonSerializer.Serialize(result);
            var content = Encoding.UTF8.GetBytes(json);
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = content.Length;
            await response.OutputStream.WriteAsync(content);
            Console.WriteLine("[API] GET /api/media-filter");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] GET /api/media-filter error: {ex.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    static async Task HandlePostMediaFilter(HttpListenerRequest request, HttpListenerResponse response)
    {
        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = await reader.ReadToEndAsync();
            var config = JsonSerializer.Deserialize<MediaFilterConfig>(body);

            if (config == null)
            {
                response.StatusCode = 400;
                var err = Encoding.UTF8.GetBytes("{\"error\":\"Invalid JSON\"}");
                response.ContentType = "application/json; charset=utf-8";
                await response.OutputStream.WriteAsync(err);
                response.Close();
                return;
            }

            var success = SaveMediaFilter(config);
            var result = Encoding.UTF8.GetBytes($"{{\"success\":{(success ? "true" : "false")}}}");
            response.StatusCode = success ? 200 : 500;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = result.Length;
            await response.OutputStream.WriteAsync(result);
            Console.WriteLine($"[API] POST /api/media-filter {(success ? "OK" : "FAILED")}");

            // Immediately apply filter - find and broadcast allowed session
            if (success)
            {
                _ = Task.Run(async () => await FindAndSendPlayingSession());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] POST /api/media-filter error: {ex.Message}");
            response.StatusCode = 400;
            var result = Encoding.UTF8.GetBytes("{\"error\":\"Invalid JSON\"}");
            response.ContentType = "application/json; charset=utf-8";
            await response.OutputStream.WriteAsync(result);
        }
        finally
        {
            response.Close();
        }
    }

    // ========================================
    // SITE AUTH HANDLERS (Cloud Sync)
    // ========================================

    static void LoadSiteAuth()
    {
        try
        {
            if (File.Exists(SITE_AUTH_FILE))
            {
                var json = File.ReadAllText(SITE_AUTH_FILE);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                _siteAuthToken = root.TryGetProperty("token", out var t) ? t.GetString() : null;
                _siteAuthNickname = root.TryGetProperty("nickname", out var n) ? n.GetString() : null;
                if (_siteAuthToken != null)
                    Console.WriteLine($"[SiteAuth] Loaded auth for: {_siteAuthNickname}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SiteAuth] Load error: {ex.Message}");
        }
    }

    static void SaveSiteAuth()
    {
        try
        {
            Directory.CreateDirectory(STYLES_DIR);
            var json = JsonSerializer.Serialize(new
            {
                token = _siteAuthToken,
                nickname = _siteAuthNickname
            }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SITE_AUTH_FILE, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SiteAuth] Save error: {ex.Message}");
        }
    }

    static async Task HandleGetSiteAuth(HttpListenerResponse response)
    {
        try
        {
            var json = JsonSerializer.Serialize(new
            {
                token = _siteAuthToken,
                nickname = _siteAuthNickname
            });
            var content = Encoding.UTF8.GetBytes(json);
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = content.Length;
            await response.OutputStream.WriteAsync(content);
            Console.WriteLine("[API] GET /api/site-auth");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] GET /api/site-auth error: {ex.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    static async Task HandlePostSiteAuth(HttpListenerRequest request, HttpListenerResponse response)
    {
        try
        {
            using var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding);
            var body = await reader.ReadToEndAsync();
            var doc = JsonDocument.Parse(body);
            var token = doc.RootElement.GetProperty("token").GetString();

            if (string.IsNullOrEmpty(token))
            {
                response.StatusCode = 400;
                var err = Encoding.UTF8.GetBytes("{\"error\":\"no token\"}");
                response.ContentType = "application/json; charset=utf-8";
                response.ContentLength64 = err.Length;
                await response.OutputStream.WriteAsync(err);
                return;
            }

            var nickname = DecodeJwtNickname(token);
            _siteAuthToken = token;
            _siteAuthNickname = nickname;
            SaveSiteAuth();

            var result = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { success = true, nickname }));
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = result.Length;
            await response.OutputStream.WriteAsync(result);
            Console.WriteLine($"[API] POST /api/site-auth - logged in as: {nickname}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] POST /api/site-auth error: {ex.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    static async Task HandleDeleteSiteAuth(HttpListenerResponse response)
    {
        try
        {
            _siteAuthToken = null;
            _siteAuthNickname = null;
            if (File.Exists(SITE_AUTH_FILE))
                File.Delete(SITE_AUTH_FILE);

            // Delete synced players (liked_*)
            try
            {
                if (Directory.Exists(CUSTOM_PLAYERS_DIR))
                {
                    foreach (var f in Directory.GetFiles(CUSTOM_PLAYERS_DIR, "liked_*"))
                    {
                        File.Delete(f);
                        Console.WriteLine($"[SiteAuth] Deleted synced player: {Path.GetFileName(f)}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SiteAuth] Error cleaning synced players: {ex.Message}");
            }

            var result = Encoding.UTF8.GetBytes("{\"success\":true}");
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = result.Length;
            await response.OutputStream.WriteAsync(result);
            Console.WriteLine("[API] DELETE /api/site-auth - logged out");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] DELETE /api/site-auth error: {ex.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    static string DecodeJwtNickname(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length < 2) return "User";
            // Base64Url decode payload
            var payload = parts[1];
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }
            var bytes = Convert.FromBase64String(payload);
            var json = Encoding.UTF8.GetString(bytes);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            // Try nickname, then user_id
            if (root.TryGetProperty("nickname", out var nick)) return nick.GetString() ?? "User";
            if (root.TryGetProperty("user_id", out var uid)) return uid.GetString() ?? "User";
            return "User";
        }
        catch { return "User"; }
    }

    // ========================================
    // FRONTEND LOGS HANDLERS
    // ========================================

    static async Task HandleFrontendLogs(HttpListenerRequest request, HttpListenerResponse response)
    {
        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = await reader.ReadToEndAsync();
            var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            string message = "";
            string level = "error";
            string? stack = null;
            string? source = null;

            if (root.TryGetProperty("message", out var msgProp)) message = msgProp.GetString() ?? "";
            if (root.TryGetProperty("level", out var levelProp)) level = levelProp.GetString() ?? "error";
            if (root.TryGetProperty("stack", out var stackProp)) stack = stackProp.GetString();
            if (root.TryGetProperty("source", out var sourceProp)) source = sourceProp.GetString();

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logEntry = stack != null
                ? $"[{timestamp}] [{level.ToUpper()}] {source}: {message}\n{stack}"
                : $"[{timestamp}] [{level.ToUpper()}] {source}: {message}";

            lock (_frontendLogsLock)
            {
                _frontendLogs.Add(logEntry);
                // Keep only last 100 entries
                if (_frontendLogs.Count > 100)
                    _frontendLogs.RemoveAt(0);
            }

            Logger.Error($"Frontend {level}: {message}");

            var result = Encoding.UTF8.GetBytes("{\"success\":true}");
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = result.Length;
            await response.OutputStream.WriteAsync(result);
            Console.WriteLine($"[FrontendLog] {level}: {message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FrontendLog] Error processing log: {ex.Message}");
            response.StatusCode = 400;
            var err = Encoding.UTF8.GetBytes("{\"error\":\"Invalid JSON\"}");
            response.ContentType = "application/json; charset=utf-8";
            await response.OutputStream.WriteAsync(err);
        }
        finally
        {
            response.Close();
        }
    }

    static async Task HandleGetFrontendLogs(HttpListenerResponse response)
    {
        try
        {
            lock (_frontendLogsLock)
            {
                var logs = _frontendLogs.ToArray();
                var json = JsonSerializer.Serialize(new { logs });
                var content = Encoding.UTF8.GetBytes(json);
                response.ContentType = "application/json; charset=utf-8";
                response.ContentLength64 = content.Length;
                response.OutputStream.WriteAsync(content);
                Console.WriteLine($"[FrontendLog] Returned {logs.Length} log entries");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FrontendLog] Error getting logs: {ex.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    static async Task HandleClearFrontendLogs(HttpListenerResponse response)
    {
        try
        {
            lock (_frontendLogsLock)
            {
                _frontendLogs.Clear();
            }

            var result = Encoding.UTF8.GetBytes("{\"success\":true}");
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = result.Length;
            await response.OutputStream.WriteAsync(result);
            Console.WriteLine("[FrontendLog] Logs cleared");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FrontendLog] Error clearing logs: {ex.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    static async Task HandleAuthCallback(HttpListenerRequest request, HttpListenerResponse response)
    {
        try
        {
            var query = request.QueryString;
            var token = query["token"];

            if (string.IsNullOrEmpty(token))
            {
                response.StatusCode = 400;
                var err = Encoding.UTF8.GetBytes("<html><body><h1>Error: no token</h1></body></html>");
                response.ContentType = "text/html; charset=utf-8";
                await response.OutputStream.WriteAsync(err);
                response.Close();
                return;
            }

            var nickname = query["nickname"] ?? DecodeJwtNickname(token);

            _siteAuthToken = token;
            _siteAuthNickname = nickname;
            SaveSiteAuth();

            var html = $@"<html>
<head><style>
  body {{ background: #0a0a0a; color: #fff; font-family: monospace; display: flex; align-items: center; justify-content: center; height: 100vh; margin: 0; }}
  .box {{ text-align: center; }}
  h1 {{ font-size: 2rem; margin-bottom: 0.5rem; }}
  p {{ color: rgba(255,255,255,0.6); }}
</style></head>
<body><div class='box'>
  <h1>Logged in</h1>
  <p>You can close this window</p>
  <script>setTimeout(()=>window.close(),2000);</script>
</div></body></html>";

            var content = Encoding.UTF8.GetBytes(html);
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = content.Length;
            await response.OutputStream.WriteAsync(content);
            Console.WriteLine($"[API] Auth callback - logged in as: {nickname}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] Auth callback error: {ex.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    // ========================================
    // CUSTOM PLAYERS HANDLERS
    // ========================================

    static async Task HandleGetCustomPlayers(HttpListenerResponse response)
    {
        try
        {
            Directory.CreateDirectory(CUSTOM_PLAYERS_DIR);
            var files = Directory.GetFiles(CUSTOM_PLAYERS_DIR, "*.html")
                .Where(f => !f.EndsWith(".backup.html"))
                .Select(f =>
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    var backupPath = Path.Combine(CUSTOM_PLAYERS_DIR, $"{name}.backup.html");
                    return new { name, hasBackup = File.Exists(backupPath), isCustom = true };
                })
                .ToList();

            var json = JsonSerializer.Serialize(new { players = files });
            var content = Encoding.UTF8.GetBytes(json);
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = content.Length;
            await response.OutputStream.WriteAsync(content);
            Console.WriteLine($"[API] GET /api/custom-players -> {files.Count} players");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] GET /api/custom-players error: {ex.Message}");
            response.StatusCode = 500;
            var result = Encoding.UTF8.GetBytes($"{{\"error\":\"{ex.Message}\"}}");
            response.ContentType = "application/json; charset=utf-8";
            await response.OutputStream.WriteAsync(result);
        }
        finally
        {
            response.Close();
        }
    }

    static async Task HandlePostCustomPlayer(HttpListenerRequest request, HttpListenerResponse response)
    {
        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = await reader.ReadToEndAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(body);

            var name = data.GetProperty("name").GetString() ?? "";
            var html = data.GetProperty("html").GetString() ?? "";

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(html))
            {
                response.StatusCode = 400;
                var err = Encoding.UTF8.GetBytes("{\"error\":\"Name and HTML are required\"}");
                response.ContentType = "application/json; charset=utf-8";
                await response.OutputStream.WriteAsync(err);
                response.Close();
                return;
            }

            // Sanitize name
            var safeName = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
            if (string.IsNullOrEmpty(safeName))
            {
                response.StatusCode = 400;
                var err = Encoding.UTF8.GetBytes("{\"error\":\"Invalid player name\"}");
                response.ContentType = "application/json; charset=utf-8";
                await response.OutputStream.WriteAsync(err);
                response.Close();
                return;
            }

            // Validate HTML
            var validation = ValidateHTML(html);
            if (!validation.valid)
            {
                response.StatusCode = 400;
                var errJson = JsonSerializer.Serialize(new { error = "HTML validation failed", validation });
                var err = Encoding.UTF8.GetBytes(errJson);
                response.ContentType = "application/json; charset=utf-8";
                await response.OutputStream.WriteAsync(err);
                response.Close();
                return;
            }

            // Save files
            Directory.CreateDirectory(CUSTOM_PLAYERS_DIR);
            var htmlPath = Path.Combine(CUSTOM_PLAYERS_DIR, $"{safeName}.html");
            var backupPath = Path.Combine(CUSTOM_PLAYERS_DIR, $"{safeName}.backup.html");

            await File.WriteAllTextAsync(htmlPath, html);

            // Save backup only on first upload
            if (!File.Exists(backupPath))
            {
                await File.WriteAllTextAsync(backupPath, html);
            }

            var result = JsonSerializer.Serialize(new { success = true, name = safeName, path = htmlPath, validation });
            var content = Encoding.UTF8.GetBytes(result);
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = content.Length;
            await response.OutputStream.WriteAsync(content);
            Console.WriteLine($"[API] POST /api/custom-players -> saved {safeName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] POST /api/custom-players error: {ex.Message}");
            response.StatusCode = 500;
            var result = Encoding.UTF8.GetBytes($"{{\"error\":\"{ex.Message}\"}}");
            response.ContentType = "application/json; charset=utf-8";
            await response.OutputStream.WriteAsync(result);
        }
        finally
        {
            response.Close();
        }
    }

    static async Task HandleGetCustomPlayer(string name, HttpListenerResponse response)
    {
        try
        {
            var safeName = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
            var htmlPath = Path.Combine(CUSTOM_PLAYERS_DIR, $"{safeName}.html");

            if (File.Exists(htmlPath))
            {
                var html = await File.ReadAllTextAsync(htmlPath);
                var content = Encoding.UTF8.GetBytes(html);
                response.ContentType = "text/html; charset=utf-8";
                response.ContentLength64 = content.Length;
                await response.OutputStream.WriteAsync(content);
                Console.WriteLine($"[API] GET /api/custom-players/{safeName}");
            }
            else
            {
                response.StatusCode = 404;
                var result = Encoding.UTF8.GetBytes("{\"error\":\"Player not found\"}");
                response.ContentType = "application/json; charset=utf-8";
                await response.OutputStream.WriteAsync(result);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] GET /api/custom-players/{name} error: {ex.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    static async Task HandleUpdateCustomPlayer(string name, HttpListenerRequest request, HttpListenerResponse response)
    {
        try
        {
            var safeName = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
            var htmlPath = Path.Combine(CUSTOM_PLAYERS_DIR, $"{safeName}.html");

            if (!File.Exists(htmlPath))
            {
                response.StatusCode = 404;
                var err = Encoding.UTF8.GetBytes("{\"error\":\"Player not found\"}");
                response.ContentType = "application/json; charset=utf-8";
                await response.OutputStream.WriteAsync(err);
                response.Close();
                return;
            }

            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = await reader.ReadToEndAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(body);
            var html = data.GetProperty("html").GetString() ?? "";

            if (string.IsNullOrEmpty(html))
            {
                response.StatusCode = 400;
                var err = Encoding.UTF8.GetBytes("{\"error\":\"HTML is required\"}");
                response.ContentType = "application/json; charset=utf-8";
                await response.OutputStream.WriteAsync(err);
                response.Close();
                return;
            }

            // Validate HTML
            var validation = ValidateHTML(html);
            if (!validation.valid)
            {
                response.StatusCode = 400;
                var errJson = JsonSerializer.Serialize(new { error = "HTML validation failed", validation });
                var err = Encoding.UTF8.GetBytes(errJson);
                response.ContentType = "application/json; charset=utf-8";
                await response.OutputStream.WriteAsync(err);
                response.Close();
                return;
            }

            await File.WriteAllTextAsync(htmlPath, html);

            var result = JsonSerializer.Serialize(new { success = true, name = safeName, validation });
            var content = Encoding.UTF8.GetBytes(result);
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = content.Length;
            await response.OutputStream.WriteAsync(content);
            Console.WriteLine($"[API] PUT /api/custom-players/{safeName} -> updated");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] PUT /api/custom-players/{name} error: {ex.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    static async Task HandleDeleteCustomPlayer(string name, HttpListenerResponse response)
    {
        try
        {
            var safeName = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
            var htmlPath = Path.Combine(CUSTOM_PLAYERS_DIR, $"{safeName}.html");
            var backupPath = Path.Combine(CUSTOM_PLAYERS_DIR, $"{safeName}.backup.html");
            var cssPath = Path.Combine(CSS_DIR, $"{safeName}.css");

            bool deleted = false;
            if (File.Exists(htmlPath)) { File.Delete(htmlPath); deleted = true; }
            if (File.Exists(backupPath)) File.Delete(backupPath);
            if (File.Exists(cssPath)) File.Delete(cssPath);

            if (deleted)
            {
                var result = Encoding.UTF8.GetBytes($"{{\"success\":true,\"name\":\"{safeName}\"}}");
                response.ContentType = "application/json; charset=utf-8";
                response.ContentLength64 = result.Length;
                await response.OutputStream.WriteAsync(result);
                Console.WriteLine($"[API] DELETE /api/custom-players/{safeName}");
            }
            else
            {
                response.StatusCode = 404;
                var result = Encoding.UTF8.GetBytes("{\"error\":\"Player not found\"}");
                response.ContentType = "application/json; charset=utf-8";
                await response.OutputStream.WriteAsync(result);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] DELETE /api/custom-players/{name} error: {ex.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    static async Task HandleGetCustomPlayerBackup(string name, HttpListenerResponse response)
    {
        try
        {
            var safeName = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
            var backupPath = Path.Combine(CUSTOM_PLAYERS_DIR, $"{safeName}.backup.html");

            if (File.Exists(backupPath))
            {
                var html = await File.ReadAllTextAsync(backupPath);
                var content = Encoding.UTF8.GetBytes(html);
                response.ContentType = "text/html; charset=utf-8";
                response.ContentLength64 = content.Length;
                await response.OutputStream.WriteAsync(content);
                Console.WriteLine($"[API] GET /api/custom-players/{safeName}/backup");
            }
            else
            {
                response.StatusCode = 404;
                var result = Encoding.UTF8.GetBytes("{\"error\":\"Backup not found\"}");
                response.ContentType = "application/json; charset=utf-8";
                await response.OutputStream.WriteAsync(result);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] GET /api/custom-players/{name}/backup error: {ex.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    static async Task HandleResetCustomPlayer(string name, HttpListenerResponse response)
    {
        try
        {
            var safeName = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
            var htmlPath = Path.Combine(CUSTOM_PLAYERS_DIR, $"{safeName}.html");
            var backupPath = Path.Combine(CUSTOM_PLAYERS_DIR, $"{safeName}.backup.html");

            if (!File.Exists(backupPath))
            {
                response.StatusCode = 404;
                var err = Encoding.UTF8.GetBytes("{\"error\":\"Backup not found\"}");
                response.ContentType = "application/json; charset=utf-8";
                await response.OutputStream.WriteAsync(err);
                response.Close();
                return;
            }

            var backupContent = await File.ReadAllTextAsync(backupPath);
            await File.WriteAllTextAsync(htmlPath, backupContent);

            var result = Encoding.UTF8.GetBytes($"{{\"success\":true,\"name\":\"{safeName}\"}}");
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = result.Length;
            await response.OutputStream.WriteAsync(result);
            Console.WriteLine($"[API] POST /api/custom-players/{safeName}/reset -> restored from backup");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] POST /api/custom-players/{name}/reset error: {ex.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    static async Task HandleValidateCustomPlayer(HttpListenerRequest request, HttpListenerResponse response)
    {
        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = await reader.ReadToEndAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(body);
            var html = data.GetProperty("html").GetString() ?? "";

            var validation = ValidateHTML(html);
            var result = JsonSerializer.Serialize(validation);
            var content = Encoding.UTF8.GetBytes(result);
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = content.Length;
            await response.OutputStream.WriteAsync(content);
            Console.WriteLine($"[API] POST /api/custom-players/validate -> valid: {validation.valid}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API] POST /api/custom-players/validate error: {ex.Message}");
            response.StatusCode = 400;
            var result = Encoding.UTF8.GetBytes("{\"error\":\"Invalid JSON\"}");
            response.ContentType = "application/json; charset=utf-8";
            await response.OutputStream.WriteAsync(result);
        }
        finally
        {
            response.Close();
        }
    }

    // ========================================
    // AUTO-UPDATE
    // ========================================

    static string ReadCurrentVersion()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "_app", "version.json"),
            Path.Combine(AppContext.BaseDirectory, "frontBuild", "_app", "version.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "frontBuild", "_app", "version.json"),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    using var doc = JsonDocument.Parse(json);
                    return doc.RootElement.GetProperty("version").GetString() ?? "";
                }
                catch { }
            }
        }
        return "";
    }

    static async Task CheckForUpdatesAsync()
    {
        try
        {
            _currentVersion ??= ReadCurrentVersion();
            Console.WriteLine($"[Update] Current version: {_currentVersion}");

            _httpUpdate.DefaultRequestHeaders.UserAgent.ParseAdd("UnikPlayer");
            var response = await _httpUpdate.GetStringAsync(GITHUB_API_URL);
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            _latestVersion = root.GetProperty("tag_name").GetString() ?? "";
            Console.WriteLine($"[Update] Latest version: {_latestVersion}");

            if (!string.IsNullOrEmpty(_currentVersion) && _latestVersion != _currentVersion)
            {
                // Find installer asset
                var assets = root.GetProperty("assets");
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        _latestInstallerUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }

                if (_latestInstallerUrl != null)
                {
                    _updateAvailable = true;
                    Console.WriteLine($"[Update] Update available: {_latestVersion}");
                    UpdateTrayIcon(true);
                }
            }
            else
            {
                Console.WriteLine("[Update] Up to date");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Update] Check failed: {ex.Message}");
        }
    }

    static void UpdateTrayIcon(bool updateAvailable)
    {
        if (_trayIcon == null) return;

        var iconPaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, updateAvailable ? "icon_update.ico" : "icon.ico"),
            Path.Combine(Directory.GetCurrentDirectory(), updateAvailable ? "icon_update.ico" : "icon.ico"),
        };

        foreach (var path in iconPaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    _trayIcon.Icon = new Icon(path);
                    break;
                }
                catch { }
            }
        }

        if (_updateMenuItem != null)
            _updateMenuItem.Visible = updateAvailable;
    }

    static async Task StartUpdate(bool relaunch)
    {
        if (_latestInstallerUrl == null) return;

        try
        {
            var tempDir = Path.GetTempPath();
            var installerPath = Path.Combine(tempDir, "UnikPlayer_Installer.exe");
            var vbsPath = Path.Combine(tempDir, "unik_update.vbs");

            // Download installer
            Console.WriteLine($"[Update] Downloading: {_latestInstallerUrl}");
            using (var response = await _httpUpdate.GetAsync(_latestInstallerUrl))
            {
                response.EnsureSuccessStatusCode();
                await using var fs = new FileStream(installerPath, FileMode.Create);
                await response.Content.CopyToAsync(fs);
            }
            Console.WriteLine("[Update] Download complete");

            // Create VBS launcher (invisible)
            var appPath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            var relaunchCmd = relaunch
                ? $"WshShell.Run \"\\\"{appPath}\\\" --autostart\", 1, False"
                : "";
            var vbsContent = $"""
                Set WshShell = CreateObject("WScript.Shell")
                WScript.Sleep 3000
                WshShell.Run "\\\"{installerPath}\\\" /S", 0, True
                {relaunchCmd}
                WScript.Sleep 2000
                Set fso = CreateObject("Scripting.FileSystemObject")
                fso.DeleteFile WScript.ScriptFullName
                """;
            await File.WriteAllTextAsync(vbsPath, vbsContent);

            // Launch VBS invisibly
            Process.Start(new ProcessStartInfo
            {
                FileName = "wscript.exe",
                Arguments = $"\"{vbsPath}\"",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            });

            Console.WriteLine("[Update] Update launched, closing...");
            await Task.Delay(500);

            _trayIcon.Visible = false;
            Application.Exit();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Update] Error: {ex.Message}");
        }
    }

    // ========================================
    // HTML VALIDATION
    // ========================================

    static readonly string[] ForbiddenTags = { "script", "iframe", "object", "embed", "applet", "form" };
    static readonly string[] EventHandlers = {
        "onabort", "onafterprint", "onbeforeprint", "onbeforeunload", "onblur",
        "oncanplay", "oncanplaythrough", "onchange", "onclick", "oncontextmenu",
        "oncopy", "oncuechange", "oncut", "ondblclick", "ondrag", "ondragend",
        "ondragenter", "ondragleave", "ondragover", "ondragstart", "ondrop",
        "ondurationchange", "onemptied", "onended", "onerror", "onfocus",
        "onhashchange", "oninput", "oninvalid", "onkeydown", "onkeypress",
        "onkeyup", "onload", "onloadeddata", "onloadedmetadata", "onloadstart",
        "onmessage", "onmousedown", "onmousemove", "onmouseout", "onmouseover",
        "onmouseup", "onmousewheel", "onoffline", "ononline", "onpagehide",
        "onpageshow", "onpaste", "onpause", "onplay", "onplaying", "onpopstate",
        "onprogress", "onratechange", "onreset", "onresize", "onscroll",
        "onsearch", "onseeked", "onseeking", "onselect", "onstalled", "onstorage",
        "onsubmit", "onsuspend", "ontimeupdate", "ontoggle", "onunload",
        "onvolumechange", "onwaiting", "onwheel"
    };
    static readonly string[] AllowedDomains = { "fonts.googleapis.com", "fonts.gstatic.com" };

    static (bool valid, List<object> errors) ValidateHTML(string html)
    {
        var errors = new List<object>();
        var lines = html.Split('\n');

        // Check file size (50KB max)
        if (Encoding.UTF8.GetByteCount(html) > 50 * 1024)
        {
            errors.Add(new { line = 1, column = 1, message = "File size exceeds 50KB limit", severity = "error" });
            return (false, errors);
        }

        // Check for script tags
        var scriptRegex = new System.Text.RegularExpressions.Regex(@"<script[\s\S]*?</script>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in scriptRegex.Matches(html))
        {
            var pos = GetLineAndColumn(html, match.Index);
            errors.Add(new { line = pos.line, column = pos.column, message = "<script> tags are not allowed", severity = "error" });
        }

        // Check for inline script tags
        var inlineScriptRegex = new System.Text.RegularExpressions.Regex(@"<script[^>]*/?\s*>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in inlineScriptRegex.Matches(html))
        {
            var pos = GetLineAndColumn(html, match.Index);
            errors.Add(new { line = pos.line, column = pos.column, message = "<script> tags are not allowed", severity = "error" });
        }

        // Check for event handlers
        foreach (var handler in EventHandlers)
        {
            var handlerRegex = new System.Text.RegularExpressions.Regex($@"\s{handler}\s*=", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (System.Text.RegularExpressions.Match match in handlerRegex.Matches(html))
            {
                var pos = GetLineAndColumn(html, match.Index);
                errors.Add(new { line = pos.line, column = pos.column, message = $"Event handler \"{handler}\" is not allowed", severity = "error" });
            }
        }

        // Check for javascript: URLs
        var jsUrlRegex = new System.Text.RegularExpressions.Regex(@"javascript\s*:", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in jsUrlRegex.Matches(html))
        {
            var pos = GetLineAndColumn(html, match.Index);
            errors.Add(new { line = pos.line, column = pos.column, message = "\"javascript:\" URLs are not allowed", severity = "error" });
        }

        // Check for forbidden tags
        foreach (var tag in ForbiddenTags)
        {
            var tagRegex = new System.Text.RegularExpressions.Regex($@"<{tag}[\s>]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (System.Text.RegularExpressions.Match match in tagRegex.Matches(html))
            {
                var pos = GetLineAndColumn(html, match.Index);
                errors.Add(new { line = pos.line, column = pos.column, message = $"<{tag}> tag is not allowed", severity = "error" });
            }
        }

        // Check external resources
        var resourceRegex = new System.Text.RegularExpressions.Regex(@"(src|href)\s*=\s*[""']([^""']+)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in resourceRegex.Matches(html))
        {
            var url = match.Groups[2].Value;
            if (url.Contains("{{") && url.Contains("}}")) continue;
            if (url.StartsWith("data:") || url.StartsWith("blob:")) continue;
            if (!url.Contains("://") && !url.StartsWith("//")) continue;

            var isAllowed = AllowedDomains.Any(domain => url.ToLower().Contains($"//{domain}/") || url.ToLower().Contains($"//{domain}"));
            if (!isAllowed)
            {
                var pos = GetLineAndColumn(html, match.Index);
                var shortUrl = url.Length > 50 ? url.Substring(0, 50) + "..." : url;
                errors.Add(new { line = pos.line, column = pos.column, message = $"External resource not allowed: {shortUrl}", severity = "error" });
            }
        }

        // Warnings for missing template variables
        if (!html.Contains("{{title}}"))
            errors.Add(new { line = 1, column = 1, message = "Missing {{title}} template variable", severity = "warning" });
        if (!html.Contains("{{artist}}"))
            errors.Add(new { line = 1, column = 1, message = "Missing {{artist}} template variable", severity = "warning" });
        if (!html.Contains("{{thumbnail}}"))
            errors.Add(new { line = 1, column = 1, message = "Missing {{thumbnail}} template variable", severity = "warning" });

        var hasErrors = errors.Any(e => ((dynamic)e).severity == "error");
        return (!hasErrors, errors);
    }

    static (int line, int column) GetLineAndColumn(string text, int index)
    {
        var lines = text.Substring(0, Math.Min(index, text.Length)).Split('\n');
        return (lines.Length, lines.Last().Length + 1);
    }

    static string GetMimeType(string ext) => ext switch
    {
        ".html" => "text/html; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".js" => "text/javascript; charset=utf-8",
        ".json" => "application/json; charset=utf-8",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".svg" => "image/svg+xml",
        ".ico" => "image/x-icon",
        ".woff" => "font/woff",
        ".woff2" => "font/woff2",
        ".ttf" => "font/ttf",
        ".otf" => "font/otf",
        _ => "application/octet-stream"
    };
}

// ������ ���� ��� ������������ ����
class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = Color.White;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var rc = new Rectangle(Point.Empty, e.Item.Size);
        var color = e.Item.Selected ? Color.FromArgb(60, 60, 60) : Color.FromArgb(32, 32, 32);
        using var brush = new SolidBrush(color);
        e.Graphics.FillRectangle(brush, rc);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var rc = new Rectangle(Point.Empty, e.Item.Size);
        using var brush = new SolidBrush(Color.FromArgb(32, 32, 32));
        e.Graphics.FillRectangle(brush, rc);

        var y = rc.Height / 2;
        using var pen = new Pen(Color.FromArgb(70, 70, 70));
        e.Graphics.DrawLine(pen, 0, y, rc.Width, y);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(Color.FromArgb(50, 50, 50));
        e.Graphics.DrawRectangle(pen, 0, 0, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1);
    }
}

class DarkColorTable : ProfessionalColorTable
{
    public override Color MenuBorder => Color.FromArgb(50, 50, 50);
    public override Color MenuItemBorder => Color.FromArgb(60, 60, 60);
    public override Color MenuItemSelected => Color.FromArgb(60, 60, 60);
    public override Color MenuStripGradientBegin => Color.FromArgb(32, 32, 32);
    public override Color MenuStripGradientEnd => Color.FromArgb(32, 32, 32);
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(60, 60, 60);
    public override Color MenuItemSelectedGradientEnd => Color.FromArgb(60, 60, 60);
    public override Color MenuItemPressedGradientBegin => Color.FromArgb(50, 50, 50);
    public override Color MenuItemPressedGradientEnd => Color.FromArgb(50, 50, 50);
    public override Color ToolStripDropDownBackground => Color.FromArgb(32, 32, 32);
    public override Color ImageMarginGradientBegin => Color.FromArgb(32, 32, 32);
    public override Color ImageMarginGradientMiddle => Color.FromArgb(32, 32, 32);
    public override Color ImageMarginGradientEnd => Color.FromArgb(32, 32, 32);
    public override Color SeparatorDark => Color.FromArgb(70, 70, 70);
    public override Color SeparatorLight => Color.FromArgb(32, 32, 32);
}
