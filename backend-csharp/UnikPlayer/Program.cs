using System.Drawing.Drawing2D;
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

    [STAThread]
    static void Main(string[] args)
    {
        // Single instance check
        bool createdNew;
        using var mutex = new Mutex(true, "UnikPlayer_SingleInstance", out createdNew);
        if (!createdNew)
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
        var path = context.Request.Url?.AbsolutePath ?? "/";
        if (path == "/") path = "/index.html";

        var filePath = Path.GetFullPath(Path.Combine(staticDir, path.TrimStart('/')));

        // Security check
        if (!filePath.StartsWith(staticDir))
        {
            context.Response.StatusCode = 403;
            context.Response.Close();
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
            context.Response.ContentType = GetMimeType(ext);
            context.Response.ContentLength64 = content.Length;
            await context.Response.OutputStream.WriteAsync(content);
        }
        catch
        {
            context.Response.StatusCode = 500;
        }
        finally
        {
            context.Response.Close();
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
