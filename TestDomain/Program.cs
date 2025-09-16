// Program.cs
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using WindowsInput;
using WindowsInput.Native;

class Program
{
    // ===== FLAGS DE DEPURACIÓN (cámbialas aquí) =====
    private const bool UseFitts = true;               // true = velocidad según distancia (Ley de Fitts), false = estático
    private const Profile ActiveProfile = Profile.Normal; // Low | Normal | Fast
    private const bool UseRealCursorOrigin = true; // true = usa GetCursorPos(); false = usa centro de la pantalla
    // ===== CONFIG BASE (se sobreescribe por perfil) =====
    private static double MouseMinSpeedPxPerSec = 700;     // si UseFitts=false, se usa rango aleatorio [Min..Max]
    private static double MouseMaxSpeedPxPerSec = 2600;
    private static double MouseJitterPx = 1.0;

    private static double ClickProbability = 0.45;
    private static double ScrollAfterClickProb = 0.30;
    private static double RightClickProb = 0.20;

    private static double TypingErrorProbability = 0.06;
    private static int TypingMinDelayMs = 50;
    private static int TypingMaxDelayMs = 220;

    // Ritmos (ajusta para “Low Activity” subiendo tiempos)
    private static int MouseMinWaitMs = 15000, MouseMaxWaitMs = 30000;
    private static int TypeMinWaitMs = 10000, TypeMaxWaitMs = 40000;
    private static int ShortMinWaitMs = 120000, ShortMaxWaitMs = 300000;
    private static int OpenMinWaitMs = 300000, OpenMaxWaitMs = 900000;
    private static int SwitchMinWaitMs = 120000, SwitchMaxWaitMs = 300000;

    // ===== Compartidos =====
    private static readonly InputSimulator Input = new();
    private static readonly SemaphoreSlim InputLock = new(1, 1);
    private static readonly CancellationTokenSource Cts = new();

    static async Task Main()
    {
        ApplyProfile(ActiveProfile);

        //Console.WriteLine($"Iniciando… (Ctrl+C para salir) | Fitts: {UseFitts} | Perfil: {ActiveProfile}");
        Console.CancelKeyPress += (s, e) => { e.Cancel = true; Cts.Cancel(); };

        await RandomDelay(7000, 20000, Cts.Token); // arranque

        var tasks = new[]
        {
            MoveMouseWithClicksAndScroll(Cts.Token),
            SimulateKeyboardActivity(Cts.Token),
            SimulateShortcuts(Cts.Token),
            OpenAndClosePrograms(Cts.Token),
            SwitchWindowsRandomly(Cts.Token)
        };

        try { await Task.WhenAll(tasks); }
        catch (OperationCanceledException) { }
        finally { InputLock.Dispose(); Cts.Dispose(); }
    }

    // ===== Perfiles =====
    enum Profile { Low, Normal, Fast }

    static void ApplyProfile(Profile p)
    {
        switch (p)
        {
            case Profile.Low:
                MouseMinSpeedPxPerSec = 400; MouseMaxSpeedPxPerSec = 1600; MouseJitterPx = 0.8;
                ClickProbability = 0.35; ScrollAfterClickProb = 0.25; RightClickProb = 0.15;
                TypingErrorProbability = 0.05; TypingMinDelayMs = 60; TypingMaxDelayMs = 240;
                MouseMinWaitMs = 20000; MouseMaxWaitMs = 45000;
                TypeMinWaitMs = 15000; TypeMaxWaitMs = 60000;
                ShortMinWaitMs = 150000; ShortMaxWaitMs = 360000;
                OpenMinWaitMs = 420000; OpenMaxWaitMs = 1020000;
                SwitchMinWaitMs = 180000; SwitchMaxWaitMs = 420000;
                break;

            case Profile.Fast:
                MouseMinSpeedPxPerSec = 900; MouseMaxSpeedPxPerSec = 3400; MouseJitterPx = 1.2;
                ClickProbability = 0.5; ScrollAfterClickProb = 0.35; RightClickProb = 0.22;
                TypingErrorProbability = 0.07; TypingMinDelayMs = 40; TypingMaxDelayMs = 180;
                MouseMinWaitMs = 8000; MouseMaxWaitMs = 18000;
                TypeMinWaitMs = 7000; TypeMaxWaitMs = 25000;
                ShortMinWaitMs = 90000; ShortMaxWaitMs = 210000;
                OpenMinWaitMs = 240000; OpenMaxWaitMs = 720000;
                SwitchMinWaitMs = 90000; SwitchMaxWaitMs = 210000;
                break;

            default: // Normal
                MouseMinSpeedPxPerSec = 700; MouseMaxSpeedPxPerSec = 2600; MouseJitterPx = 1.0;
                ClickProbability = 0.45; ScrollAfterClickProb = 0.30; RightClickProb = 0.20;
                TypingErrorProbability = 0.06; TypingMinDelayMs = 50; TypingMaxDelayMs = 220;
                MouseMinWaitMs = 15000; MouseMaxWaitMs = 30000;
                TypeMinWaitMs = 10000; TypeMaxWaitMs = 40000;
                ShortMinWaitMs = 120000; ShortMaxWaitMs = 300000;
                OpenMinWaitMs = 300000; OpenMaxWaitMs = 900000;
                SwitchMinWaitMs = 120000; SwitchMaxWaitMs = 300000;
                break;
        }
    }

    // ===== Helpers SO =====
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);
    private const int SM_CXSCREEN = 0, SM_CYSCREEN = 1;
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    static (int x, int y) GetCursorPosition()
    {
        return GetCursorPos(out var p) ? (p.X, p.Y) : (GetScreenSize().w / 2, GetScreenSize().h / 2);
    }

    static bool IsCursorInActiveWindow() => GetForegroundWindow() != IntPtr.Zero;
    static (int w, int h) GetScreenSize() => (GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));

    static async Task WithInputAsync(Func<InputSimulator, Task> action)
    {
        await InputLock.WaitAsync();
        try { await action(Input); }
        finally { InputLock.Release(); }
    }

    static async Task RandomDelay(int minMs, int maxMs, CancellationToken ct, double idleProbability = 0.35)
    {
        var rnd = Random.Shared;
        var factor = rnd.NextDouble() < idleProbability ? 2 : 1;
        await Task.Delay(rnd.Next(minMs * factor, maxMs * factor), ct);
    }

    // ===== [1] Mouse humano (Bézier + jitter + Fitts opcional) =====
    static async Task MoveMouseWithClicksAndScrollDeprecated(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await RandomDelay(MouseMinWaitMs, MouseMaxWaitMs, ct, idleProbability: 0.4);
            if (!IsCursorInActiveWindow()) continue;

            var rnd = Random.Shared;
            var (w, h) = GetScreenSize();

            // destino evitando bordes
            int endX = rnd.Next(w / 6, (w * 5) / 6);
            int endY = rnd.Next(h / 6, (h * 5) / 6);

            // origen simple (centro). Puedes cambiarlo por posición real del cursor si quieres.
            var from = (w / 2, h / 2);
            var to = (endX, endY);

            await WithInputAsync(async sim =>
            {
                await MoveMouseSmooth(sim, from, to, ct);

                if (rnd.NextDouble() < ClickProbability)
                {
                    if (rnd.NextDouble() < RightClickProb) sim.Mouse.RightButtonClick();
                    else sim.Mouse.LeftButtonClick();

                    if (rnd.NextDouble() < ScrollAfterClickProb)
                        sim.Mouse.VerticalScroll(rnd.Next(1, 5) * (rnd.NextDouble() < 0.7 ? -1 : 1));
                }
            });
        }
    }
    static async Task MoveMouseWithClicksAndScroll(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await RandomDelay(MouseMinWaitMs, MouseMaxWaitMs, ct, idleProbability: 0.4);
            if (!IsCursorInActiveWindow()) continue;

            var rnd = Random.Shared;
            var (w, h) = GetScreenSize();

            // destino evitando bordes
            int endX = rnd.Next(w / 6, (w * 5) / 6);
            int endY = rnd.Next(h / 6, (h * 5) / 6);

            // ORIGEN: real o centro, según flag
            var origin = UseRealCursorOrigin ? GetCursorPosition() : (w / 2, h / 2);
            // clamp por si el cursor está fuera del primario (multi-monitor)
            var from = (Math.Clamp(origin.Item1, 0, w - 1), Math.Clamp(origin.Item2, 0, h - 1));
            var to = (endX, endY);

            await WithInputAsync(async sim =>
            {
                await MoveMouseSmooth(sim, from, to, ct);

                if (rnd.NextDouble() < ClickProbability)
                {
                    if (rnd.NextDouble() < RightClickProb) sim.Mouse.RightButtonClick();
                    else sim.Mouse.LeftButtonClick();

                    if (rnd.NextDouble() < ScrollAfterClickProb)
                        sim.Mouse.VerticalScroll(rnd.Next(1, 5) * (rnd.NextDouble() < 0.7 ? -1 : 1));
                }
            });
        }
    }


    static async Task MoveMouseSmooth(InputSimulator sim, (int x, int y) from, (int x, int y) to, CancellationToken ct)
    {
        var rnd = Random.Shared;
        var (w, h) = GetScreenSize();

        double dx = to.x - from.x, dy = to.y - from.y;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        // ====== Selección de velocidad ======
        double speed;
        if (UseFitts)
        {
            // Ley de Fitts inspirada: más distancia => más rápido (normaliza con ~60% de la diagonal)
            double diag = Math.Sqrt(w * w + h * h);
            double norm = Math.Clamp(distance / (0.6 * diag), 0.0, 1.0);
            speed = MouseMinSpeedPxPerSec + norm * (MouseMaxSpeedPxPerSec - MouseMinSpeedPxPerSec);
        }
        else
        {
            // Estático (aleatorio dentro del rango)
            speed = MouseMinSpeedPxPerSec + rnd.NextDouble() * (MouseMaxSpeedPxPerSec - MouseMinSpeedPxPerSec);
        }

        double duration = Math.Max(150, distance / Math.Max(1.0, speed) * 1000.0);

        var p0 = new Vector2(from.x, from.y);
        var p3 = new Vector2(to.x, to.y);

        // control points para curva Bézier con componente perpendicular
        var perp = new Vector2((float)-dy, (float)dx);
        if (perp.Length() > 0.0001f) perp = Vector2.Normalize(perp);
        float magnitude = (float)(Math.Min(200, distance) * (0.15 + rnd.NextDouble() * 0.35));
        var p1 = p0 + (p3 - p0) * 0.30f + perp * magnitude;
        var p2 = p0 + (p3 - p0) * 0.65f - perp * magnitude * 0.6f;

        int steps = Math.Clamp((int)(duration / 8), 6, 300);
        var sw = Stopwatch.StartNew();

        for (int i = 1; i <= steps; i++)
        {
            if (ct.IsCancellationRequested) break;

            float t = (float)i / steps;
            // easing in-out
            t = t < 0.5f ? (float)(2 * t * t) : (float)(-1 + (4 - 2 * t) * t);

            var point = CubicBezier(p0, p1, p2, p3, t);

            // jitter proporcional (más en trazos largos)
            double diag = Math.Sqrt(w * w + h * h);
            double norm = Math.Clamp(distance / (0.6 * diag), 0.0, 1.0);
            double jitter = MouseJitterPx * (0.6 + 0.8 * norm);

            point.X += (float)(rnd.NextDouble() * 2 * jitter - jitter);
            point.Y += (float)(rnd.NextDouble() * 2 * jitter - jitter);

            double ax = point.X * 65535.0 / Math.Max(1, w - 1);
            double ay = point.Y * 65535.0 / Math.Max(1, h - 1);
            sim.Mouse.MoveMouseTo(ax, ay);

            // pacing para respetar la duración total
            double target = duration * t;
            double sleep = target - sw.Elapsed.TotalMilliseconds;
            if (sleep > 0) await Task.Delay((int)sleep, ct);
            else await Task.Yield();
        }
    }

    static Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1 - t;
        return (u * u * u) * p0
             + (3 * u * u * t) * p1
             + (3 * u * t * t) * p2
             + (t * t * t) * p3;
    }

    // ===== [2] Teclado con errores simulados =====
    static async Task SimulateKeyboardActivity(CancellationToken ct)
    {
        var phrases = new[]
        {
            "var list = new List<string>();",
            "Console.WriteLine(\"Hello, world!\");",
            "public class Response<T> { public T? Data { get; set; } }",
            "git status",
            "dotnet build --configuration Debug",
            "docker ps -a",
            $"// DEBUG: Variable value = {Random.Shared.Next(1,100)}",
            // ==== C# Snippets ====
            "app.MapGet(\"/health\", () => Results.Ok(new { Status = \"Healthy\" }));",
            "builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connStr));",
            "record User(int Id, string Name, string Email);",
            "await using var scope = provider.CreateAsyncScope();",
            "var token = jwtHandler.CreateToken(userClaims);",
            "return Results.BadRequest(new { Error = \"Invalid credentials\" });",
            "if (!ModelState.IsValid) return BadRequest(ModelState);",
            "logger.LogInformation(\"Processing request {Id}\", id);",

            // ==== LINQ / EF Core ====
            "var users = await context.Users.Where(u => u.IsActive).ToListAsync();",
            "bool exists = await context.Orders.AnyAsync(o => o.Id == orderId);",
            "var stats = data.GroupBy(x => x.Type).Select(g => new { g.Key, Count = g.Count() });",

            // ==== Comments ====
            "// TODO: Refactor this service",
            "// FIXME: Handle null reference properly",
            "// NOTE: Validate JWT expiration",
            $"// DEBUG: Iteration {Random.Shared.Next(1, 100)}",
            "/* OPTIMIZE: Index missing on Email column */",

            // ==== Terminal commands ====
            "git pull origin main",
            "dotnet watch run",
            "dotnet ef migrations add Init",
            "dotnet ef database update",
            "docker compose up -d",
            "curl http://localhost:5000/health",
            "ls -la",
            "ps aux | grep dotnet",

            // ==== Config snippets ====
            "{ \"Logging\": { \"LogLevel\": { \"Default\": \"Information\" } } }",
            "builder.Services.AddCors(o => o.AllowAnyOrigin());",
            "app.UseHttpsRedirection();",
        };

        while (!ct.IsCancellationRequested)
        {
            await RandomDelay(TypeMinWaitMs, TypeMaxWaitMs, ct, idleProbability: 0.3);
            if (!IsCursorInActiveWindow()) continue;
            if (Random.Shared.Next(100) >= 70) continue; // 30% de escribir

            var text = phrases[Random.Shared.Next(phrases.Length)];

            foreach (char c in text)
            {
                if (!IsCursorInActiveWindow()) break;

                // error ocasional
                if (Random.Shared.NextDouble() < TypingErrorProbability && char.IsLetterOrDigit(c))
                {
                    char wrong = RandomLetterOrDigit();
                    await WithInputAsync(sim => { sim.Keyboard.TextEntry(wrong.ToString()); return Task.CompletedTask; });
                    await Task.Delay(Random.Shared.Next(100, 300), ct);
                    await WithInputAsync(sim => { sim.Keyboard.KeyPress(VirtualKeyCode.BACK); return Task.CompletedTask; });
                    await Task.Delay(Random.Shared.Next(80, 200), ct);
                }

                await WithInputAsync(sim => { sim.Keyboard.TextEntry(c.ToString()); return Task.CompletedTask; });

                int delay = Random.Shared.Next(TypingMinDelayMs, TypingMaxDelayMs);
                if (c is ' ' or '.' or ',' or ';') delay += Random.Shared.Next(80, 300);
                await Task.Delay(delay, ct);
            }

            await Task.Delay(Random.Shared.Next(500, 3000), ct);
        }
    }

    static char RandomLetterOrDigit()
    {
        int r = Random.Shared.Next(0, 36);
        return r < 10 ? (char)('0' + r) : (char)('a' + (r - 10));
    }

    // ===== [3] Atajos =====
    static async Task SimulateShortcuts(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await RandomDelay(ShortMinWaitMs, ShortMaxWaitMs, ct);
            if (!IsCursorInActiveWindow()) continue;

            var pick = Random.Shared.Next(0, 4);
            await WithInputAsync(sim =>
            {
                switch (pick)
                {
                    case 1: sim.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_C); break;
                    case 2: sim.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.TAB); break;
                    case 3: sim.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, new[] { VirtualKeyCode.TAB, VirtualKeyCode.TAB }); break;
                        // case 0: nada
                }
                return Task.CompletedTask;
            });
        }
    }

    // ===== [4] Abrir/Cerrar programas =====
    static async Task OpenAndClosePrograms(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await RandomDelay(OpenMinWaitMs, OpenMaxWaitMs, ct);
            if (ct.IsCancellationRequested) break;

            string program = Random.Shared.Next(0, 4) switch
            {
                0 => "notepad.exe",
                1 => "cmd.exe",
                2 => "chrome.exe",
                _ => "code" // VS Code
            };

            try
            {
                using var p = Process.Start(program);
                await Task.Delay(3000, ct);

                await WithInputAsync(sim =>
                {
                    switch (program)
                    {
                        case "notepad.exe":
                            sim.Keyboard.TextEntry($"Notas del día: {DateTime.Now:dd/MM/yyyy}\n");
                            break;
                        case "cmd.exe":
                            sim.Keyboard.TextEntry("dir\n");
                            break;
                        case "chrome.exe":
                            sim.Keyboard.TextEntry("https://www.google.com/search?q=dotnet+8\n");
                            break;
                    }
                    return Task.CompletedTask;
                });

                await Task.Delay(Random.Shared.Next(60000, 180000), ct); // 1–3 min
                await WithInputAsync(sim =>
                {
                    sim.Keyboard.ModifiedKeyStroke(VirtualKeyCode.MENU, VirtualKeyCode.F4); // Alt+F4
                    return Task.CompletedTask;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"No pude iniciar {program}: {ex.Message}");
            }
        }
    }

    // ===== [5] Cambio de ventanas =====
    static async Task SwitchWindowsRandomly(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await RandomDelay(SwitchMinWaitMs, SwitchMaxWaitMs, ct);
            if (!IsCursorInActiveWindow()) continue;

            int hops = Random.Shared.Next(1, 4); // 1–3 tabs
            await WithInputAsync(sim =>
            {
                sim.Keyboard.KeyDown(VirtualKeyCode.LMENU);
                for (int i = 0; i < hops; i++)
                {
                    sim.Keyboard.KeyPress(VirtualKeyCode.TAB);
                    Task.Delay(100).Wait();
                }
                sim.Keyboard.KeyUp(VirtualKeyCode.LMENU);
                return Task.CompletedTask;
            });

            await Task.Delay(Random.Shared.Next(500, 1500), ct);
        }
    }
}
