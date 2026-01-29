using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Net;
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
    private static string? _activeSessionId;  // ID последней активной сессии
    private static readonly int DEBOUNCE_MS = 300;
    private static readonly int HTTP_PORT = 27272;
    private static readonly int WS_PORT = 62727;

    // Data paths - initialized in Main based on DEV_MODE
    private static string STYLES_DIR = "";
    private static string STYLES_FILE = "";
    private static string CSS_DIR = "";
    private static bool DEV_MODE = false;

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

        // Ensure directories exist
        Directory.CreateDirectory(STYLES_DIR);
        Directory.CreateDirectory(CSS_DIR);
    }

    [STAThread]
    static void Main(string[] args)
    {
        // Single instance check (skip in dev mode)
        bool createdNew;
        using var mutex = new Mutex(true, "UnikPlayer_SingleInstance", out createdNew);

        // Initialize paths first to check DEV_MODE
        InitializePaths();

        if (!createdNew && !DEV_MODE)
        {
            Console.WriteLine("UnikPlayer уже запущен!");
            return;
        }

        Console.WriteLine("UnikPlayer запускается...");

        // Start services
        Task.Run(() => StartHttpServer());
        Task.Run(() => StartWebSocketServer());
        StartMediaManager();

        Console.WriteLine($"HTTP сервер: http://localhost:{HTTP_PORT}/");
        Console.WriteLine($"WebSocket сервер: ws://localhost:{WS_PORT}/");
        Console.WriteLine("UnikPlayer работает.");

        // Setup tray icon
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        SetupTrayIcon();

        // Open browser if not autostart
        if (!args.Contains("--autostart"))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = $"http://localhost:{HTTP_PORT}/",
                UseShellExecute = true
            });
        }

        Application.Run();

        // Cleanup
        _mediaManager?.Dispose();
        _trayIcon?.Dispose();
        _httpListener?.Stop();
        _wsListener?.Stop();
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

                    // Делаем иконку белой
                    if (makeWhite)
                    {
                        SetSvgColor(svgDoc, new SvgColourServer(Color.White));
                    }

                    svgDoc.Width = size;
                    svgDoc.Height = size;
                    var bitmap = svgDoc.Draw(size, size);
                    Console.WriteLine($"[Tray] SVG загружен: {path}");
                    return bitmap;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Tray] Ошибка загрузки SVG {path}: {ex.Message}");
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

        // Ищем иконку в разных местах
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
                    Console.WriteLine($"[Tray] Иконка загружена: {path}");
                    break;
                }
                catch { }
            }
        }

        _trayIcon.Icon = appIcon ?? SystemIcons.Application;

        // Загружаем SVG иконки для меню (белые для темной темы)
        var homeIcon = LoadSvgAsBitmap("home.svg", 16, makeWhite: true);
        var exitIcon = LoadSvgAsBitmap("exit.svg", 16, makeWhite: true);

        var menu = new ContextMenuStrip();

        // Темная тема для меню
        menu.Renderer = new DarkMenuRenderer();
        menu.BackColor = Color.FromArgb(32, 32, 32);
        menu.ForeColor = Color.White;

        // Кнопка "Open site" с иконкой
        var openItem = new ToolStripMenuItem("Open site", homeIcon, (s, e) =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = $"http://localhost:{HTTP_PORT}/",
                UseShellExecute = true
            });
        });
        openItem.ForeColor = Color.White;
        menu.Items.Add(openItem);

        menu.Items.Add(new ToolStripSeparator());

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
                FileName = $"http://localhost:{HTTP_PORT}/",
                UseShellExecute = true
            });
        };
    }

    static void StartMediaManager()
    {
        _mediaManager = new MediaManager();

        _mediaManager.OnAnyMediaPropertyChanged += async (session, args) =>
        {
            // Обновляем только если это активная сессия или нет активной
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
                // Сессия начала играть - делаем её активной
                _activeSessionId = session.Id;
                Console.WriteLine($"[SMTC] Активная сессия: {session.Id}");
                await SendMediaUpdate(session);
            }
            else
            {
                // Сессия остановилась
                if (_activeSessionId == session.Id)
                {
                    // Это была активная сессия - ищем другую играющую
                    await FindAndSendPlayingSession();
                }
            }
        };

        _mediaManager.OnAnySessionOpened += async (session) =>
        {
            // Новая сессия - проверяем играет ли она
            var playback = session.ControlSession.GetPlaybackInfo();
            if (playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                _activeSessionId = session.Id;
                await SendMediaUpdate(session);
            }
        };

        _mediaManager.OnAnySessionClosed += async (session) =>
        {
            if (_activeSessionId == session.Id)
            {
                _activeSessionId = null;
                await FindAndSendPlayingSession();
            }
        };

        _mediaManager.Start();
        Console.WriteLine("SMTC слушатель запущен");
    }

    static async Task FindAndSendPlayingSession()
    {
        if (_mediaManager == null) return;

        // Ищем любую сессию со статусом Playing
        foreach (var session in _mediaManager.CurrentMediaSessions.Values)
        {
            try
            {
                var playback = session.ControlSession.GetPlaybackInfo();
                if (playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                {
                    _activeSessionId = session.Id;
                    await SendMediaUpdate(session);
                    return;
                }
            }
            catch { }
        }

        // Нет играющих сессий - отправляем null и скрываем
        _activeSessionId = null;
        _lastFingerprint = null;
        _lastSentJson = null;
        BroadcastMessage(JsonSerializer.Serialize(new { media = (object?)null }));
        Console.WriteLine("[SMTC] Нет активных сессий - скрываем плеер");
    }

    static async Task SendMediaUpdate(MediaManager.MediaSession session)
    {
        const int RETRY_DELAY_MS = 500;

        try
        {
            var playback = session.ControlSession.GetPlaybackInfo();

            if (playback.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                return;
            }

            var mediaProps = await session.ControlSession.TryGetMediaPropertiesAsync();

            // Проверяем что есть все данные
            var title = mediaProps.Title ?? "";
            var artist = mediaProps.Artist ?? "";

            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(artist))
            {
                // Нет title или artist - пробуем ещё раз
                Console.WriteLine($"[SMTC] Нет title/artist, retry...");
                await Task.Delay(RETRY_DELAY_MS);
                await SendMediaUpdate(session);
                return;
            }

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

            // Если нет thumbnail - пробуем ещё раз
            if (thumbnailData == null)
            {
                // Проверяем что сессия всё ещё активна и играет
                var currentPlayback = session.ControlSession.GetPlaybackInfo();
                if (currentPlayback.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                {
                    return;
                }

                Console.WriteLine($"[SMTC] Нет thumbnail для {artist} - {title}, retry...");
                await Task.Delay(RETRY_DELAY_MS);
                await SendMediaUpdate(session);
                return;
            }

            var fingerprint = $"{session.Id}||{title}||{artist}";

            // Debounce: skip if same fingerprint and too soon
            var now = DateTime.Now;
            if (fingerprint == _lastFingerprint && (now - _lastSentTime).TotalMilliseconds < DEBOUNCE_MS)
            {
                return;
            }

            var data = new
            {
                media = new
                {
                    title,
                    artist,
                    thumbnail = new { data = thumbnailData }
                }
            };

            var json = JsonSerializer.Serialize(data);

            // Skip if exact same JSON
            if (json == _lastSentJson)
            {
                return;
            }

            _lastFingerprint = fingerprint;
            _lastSentJson = json;
            _lastSentTime = now;
            BroadcastMessage(json);
            Console.WriteLine($"[SMTC] {artist} - {title}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SMTC] Error: {ex.Message}");
        }
    }

    static void BroadcastMessage(string message)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        var segment = new ArraySegment<byte>(buffer);

        lock (_lock)
        {
            var deadClients = new List<WebSocket>();
            foreach (var client in _clients)
            {
                try
                {
                    if (client.State == WebSocketState.Open)
                    {
                        client.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None).Wait();
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

            foreach (var dead in deadClients)
            {
                _clients.Remove(dead);
            }
        }
    }

    static async Task StartWebSocketServer()
    {
        _wsListener = new HttpListener();
        _wsListener.Prefixes.Add($"http://localhost:{WS_PORT}/");
        _wsListener.Start();
        Console.WriteLine($"[WS] WebSocket сервер запущен на порту {WS_PORT}");

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

    static async Task SendCurrentStateToClient(WebSocket ws)
    {
        const int RETRY_DELAY_MS = 500;

        if (_mediaManager == null) return;

        // Ищем играющую сессию
        foreach (var session in _mediaManager.CurrentMediaSessions.Values)
        {
            try
            {
                var playback = session.ControlSession.GetPlaybackInfo();
                if (playback.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
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

                // Если нет thumbnail - retry бесконечно
                if (thumbnailData == null)
                {
                    if (ws.State == WebSocketState.Open)
                    {
                        Console.WriteLine($"[WS] Нет thumbnail для нового клиента, retry...");
                        await Task.Delay(RETRY_DELAY_MS);
                        await SendCurrentStateToClient(ws);
                    }
                    return;
                }

                var data = new
                {
                    media = new
                    {
                        title,
                        artist,
                        thumbnail = new { data = thumbnailData }
                    }
                };

                var json = JsonSerializer.Serialize(data);
                var buffer = Encoding.UTF8.GetBytes(json);
                var segment = new ArraySegment<byte>(buffer);

                if (ws.State == WebSocketState.Open)
                {
                    await ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                    Console.WriteLine($"[WS] Отправлено текущее состояние: {artist} - {title}");
                }
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WS] Ошибка получения состояния: {ex.Message}");
            }
        }

        // Нет играющих сессий - retry бесконечно пока клиент подключен
        if (ws.State == WebSocketState.Open)
        {
            Console.WriteLine($"[WS] Нет активных сессий для нового клиента, retry...");
            await Task.Delay(RETRY_DELAY_MS);
            await SendCurrentStateToClient(ws);
        }
    }

    static async Task HandleWebSocketClient(HttpListenerContext context)
    {
        var wsContext = await context.AcceptWebSocketAsync(null);
        var ws = wsContext.WebSocket;

        lock (_lock)
        {
            _clients.Add(ws);
        }
        Console.WriteLine("[WS] Клиент подключился");

        // Отправляем текущее состояние новому клиенту
        await SendCurrentStateToClient(ws);

        var buffer = new byte[1024];
        try
        {
            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                }
            }
        }
        catch { }
        finally
        {
            lock (_lock)
            {
                _clients.Remove(ws);
            }
            Console.WriteLine("[WS] Клиент отключился");
        }
    }

    static async Task StartHttpServer()
    {
        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add($"http://localhost:{HTTP_PORT}/");
        _httpListener.Start();
        Console.WriteLine($"[HTTP] Сервер запущен на порту {HTTP_PORT}");

        // Find frontBuild directory
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "frontBuild"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "frontBuild"),
            Path.Combine(AppContext.BaseDirectory, "frontBuild"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "frontBuild"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "frontBuild"),
            Path.Combine(Directory.GetCurrentDirectory(), "frontBuild"),
            @"C:\Users\000-d\Desktop\JShit\unikPlayer\frontBuild"
        };

        string? staticDir = null;
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
            Console.WriteLine("[HTTP] ОШИБКА: frontBuild не найден!");
            return;
        }

        Console.WriteLine($"[HTTP] Serving: {staticDir}");

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

    static async Task HandleHttpRequest(HttpListenerContext context, string staticDir)
    {
        var request = context.Request;
        var response = context.Response;
        var path = request.Url?.AbsolutePath ?? "/";

        // CORS headers for dev mode
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
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

        // API: GET /api/open-css/{playerName} - open CSS file in explorer
        if (path.StartsWith("/api/open-css/") && request.HttpMethod == "GET")
        {
            var playerName = path.Substring("/api/open-css/".Length);
            await HandleOpenCSS(playerName, response);
            return;
        }

        // Static file serving
        if (path == "/") path = "/index.html";

        var filePath = Path.GetFullPath(Path.Combine(staticDir, path.TrimStart('/')));

        // Security check
        if (!filePath.StartsWith(staticDir))
        {
            response.StatusCode = 403;
            response.Close();
            return;
        }

        // If file not found, serve index.html (SPA routing)
        if (!File.Exists(filePath))
        {
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
        _ => "application/octet-stream"
    };
}

// Темная тема для контекстного меню
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
