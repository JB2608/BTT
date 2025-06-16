using WindowsInput;
using WindowsInput.Native;
using System.Runtime.InteropServices;
using System.Diagnostics;

class Program
{
    static async Task Main(string[] args)
    {
        await RandomDelay(10000, 30000); // Delay inicial aleatorio (10-30 segundos)

        var tasks = new List<Task>
        {
            Task.Run(() => MoveMouseWithClicksAndScroll()),
            Task.Run(() => SimulateKeyboardActivity()),
            Task.Run(() => SimulateShortcuts()),
            Task.Run(() => OpenAndClosePrograms()),
            Task.Run(() => SwitchWindowsRandomly())
        };

        await Task.WhenAll(tasks);
    }

    // ===== [1. Mouse: Movimiento + Clicks + Scroll + Arrastrar] =====
    static async Task MoveMouseWithClicksAndScroll()
    {
        var input = new InputSimulator();
        var random = new Random();

        while (true)
        {
            await RandomDelay(20000, 60000, idleProbability: 0.4); // 40% idle

            if (IsCursorInActiveWindow())
            {
                // Movimiento a posición aleatoria
                int x = random.Next(500, 1500);
                int y = random.Next(300, 800);
                input.Mouse.MoveMouseTo(x * 65535 / 1920, y * 65535 / 1080);

                // 50% de probabilidad de acción
                if (random.Next(0, 100) < 50)
                {
                    switch (random.Next(0, 5))
                    {
                        case 0: // Click izquierdo
                            input.Mouse.LeftButtonClick();
                            break;
                        case 1: // Click derecho
                            input.Mouse.RightButtonClick();
                            break;
                        case 2: // Scroll (70% abajo)
                            input.Mouse.VerticalScroll(random.Next(0, 100) < 10 ? -10 : 5);
                            break;
                        case 3: // Arrastrar y soltar
                            //input.Mouse.LeftButtonDown();
                            //await Task.Delay(random.Next(300, 800));
                            //input.Mouse.MoveMouseTo((x + 100) * 65535 / 1920, (y + 50) * 65535 / 1080);
                            //input.Mouse.LeftButtonUp();
                            input.Mouse.VerticalScroll(random.Next(0, 100) < 15 ? -10 : 5);
                            break;
                        case 4: // Scroll (70% abajo)
                            input.Mouse.VerticalScroll(random.Next(0, 100) < 50 ? -10 : 5);
                            break;
                        case 5: // Scroll (70% abajo)
                            input.Mouse.VerticalScroll(random.Next(0, 100) < 20 ? -10 : 5);
                            break;
                    }
                }
            }
        }
    }

    // ===== [2. Teclado: Frases seguras + Errores simulados] =====
    static async Task SimulateKeyboardActivity()
    {
        var input = new InputSimulator();
        var random = new Random();

        var phrases = new List<string>
        {
            // ========== [Código C#] ==========
            "var list = new List<string>();",
            "Console.WriteLine(\"Hello, world!\");",
            "public class Response<T> { public T? Data { get; set; } }",
            "try { await _service.ProcessAsync(); } catch (Exception ex) { _logger.LogError(ex, \"Error\"); }",
            "services.AddScoped<IAuthService, AuthService>();",
            "return Results.Ok(new { Message = \"Success\" });",
            "var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);",
            "HttpClient client = new HttpClient();",
            "[HttpGet(\"{id}\")] public IActionResult GetById(int id) => Ok(_repository.Get(id));",
            "public record UserDto(string Name, int Age);",
            "app.MapPost(\"/login\", async (LoginDto dto, IAuthService auth) => await auth.LoginAsync(dto));",
            "builder.Services.AddEndpointsApiExplorer();",
            "builder.Services.AddSwaggerGen();",
            "if (!ModelState.IsValid) return BadRequest(ModelState);",
            "[Authorize] public class SecureController : ControllerBase { }",
            "var configuration = builder.Configuration;",
            "await context.SaveChangesAsync();",
            "var token = jwtHandler.GenerateToken(user);",
            "app.UseAuthentication();",
            "app.UseAuthorization();",
            "return Results.BadRequest(new { Error = \"Invalid credentials\" });",
            "builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(\"Jwt\"));",
            "public interface IRepository<T> where T : class { Task<T?> GetByIdAsync(int id); }",
            "app.MapPut(\"/update\", async (UserDto user, IUserService service) => await service.UpdateAsync(user));",
            "[FromQuery] string? filter, [FromBody] UserDto dto",
            "var exists = await _context.Users.AnyAsync(u => u.Email == dto.Email);",

            // ========== [Comandos Terminal] ==========
            "git status",
            "dotnet build --configuration Debug",
            "docker ps -a",
            "npm install",
            "cd ~/projects/my-app",
            "ls -la",
            "ssh user@server -p 2222",
            "kubectl get pods --namespace dev",
            "az account list",

            

            // ========== [Comentarios/TODOs] ==========
            "// TODO: Refactorizar este módulo",
            "/* FIXME: Handle null exception here */",
            "# TEMPORAL: Eliminar después del release",
            "<!-- WIP: Falta añadir el formulario -->",
            "// DEBUG: Variable value = " + new Random().Next(1, 100),
            "/* OPTIMIZE: Esta query necesita índices */",

            //// ========== [SQL] ==========
            //"SELECT * FROM Products WHERE Price > 100;",
            //"UPDATE Orders SET Status = 'Shipped' WHERE Id = 123;",
            //"INSERT INTO Users (Name, Email) VALUES ('Alice', 'alice@example.com');",
            //"CREATE INDEX IX_Users_Email ON Users(Email);",

            // ========== [Configuraciones] ==========
            "{\"Logging\": {\"LogLevel\": {\"Default\": \"Information\"}}}",
            "builder.Services.AddCors(options => options.AllowAnyOrigin());",
            "app.UseHttpsRedirection();",
            "connectionString: \"Server=localhost;Database=MyDb;Trusted_Connection=True;\"",

            // ========== [Atajos/Notas] ==========
            //"// Ctrl+K Ctrl+D: Formatear documento",
            //"// F5: Start Debugging",
        };

        while (true)
        {
            await RandomDelay(300000, 600000, idleProbability: 0.3); // Espera inicial

            if (IsCursorInActiveWindow() && random.Next(0, 100) < 70)
            {
                string text = phrases[random.Next(phrases.Count)];

                // Escribe carácter por carácter
                foreach (char c in text)
                {
                    if (!IsCursorInActiveWindow()) break; // Si la ventana pierde foco, detén la escritura

                    input.Keyboard.TextEntry(c.ToString());

                    // Delay entre caracteres (base: 50-200ms)
                    int delay = random.Next(50, 200);

                    // Pausa más larga después de signos de puntuación
                    if (c == '.' || c == ',' || c == ';')
                        delay += random.Next(200, 500);

                    await Task.Delay(delay);
                }

                // Delay después de escribir la frase completa
                await Task.Delay(random.Next(500, 3000));

                // 30% de probabilidad de presionar Enter al final
                if (random.Next(0, 100) < 30)
                {
                    input.Keyboard.KeyPress(VirtualKeyCode.RETURN);
                    await Task.Delay(random.Next(1000, 2000));
                }
            }
        }
    }
    
    

    // ===== [3. Atajos de teclado comunes] =====
    static async Task SimulateShortcuts()
    {
        var input = new InputSimulator();
        var random = new Random();

        while (true)
        {
            await RandomDelay(60000, 300000); // 2-5 minutos

            if (IsCursorInActiveWindow())
            {
                switch (random.Next(0, 4))
                {
                    case 0: // Ctrl + S (Guardar)
                        input.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_S);
                        break;
                    case 1: // Ctrl + C (Copiar)
                        input.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_C);
                        break;
                    case 2: // Ctrl + C (Copiar)
                        input.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.TAB);
                        break;
                    case 3: // Ctrl + C (Copiar)
                        input.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, new[] { VirtualKeyCode.TAB, VirtualKeyCode.TAB });
                        break;
                    case 4: // Ctrl + C (Copiar)
                        input.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, new[] { VirtualKeyCode.TAB, VirtualKeyCode.TAB, VirtualKeyCode.TAB });
                        break;
                        //case 2: // Win + D (Mostrar escritorio)
                        //    input.Keyboard.ModifiedKeyStroke(VirtualKeyCode.LWIN, VirtualKeyCode.VK_D);
                        //    break;
                }
            }
        }
    }

    // ===== [4. Abrir/Cerrar programas con interacción] =====
    static async Task OpenAndClosePrograms()
    {
        var input = new InputSimulator();
        var random = new Random();

        while (true)
        {
            await RandomDelay(300000, 900000); // 5-15 minutos

            string program = random.Next(0, 4) switch
            {
                0 => "notepad.exe",
                1 => "cmd.exe",
                2 => "chrome.exe",
                _ => "code" // VS Code
            };

            Process.Start(program);
            await Task.Delay(3000);

            // Interacción dentro del programa
            switch (program)
            {
                case "notepad.exe":
                    input.Keyboard.TextEntry("Notas del día: " + DateTime.Now.ToString("dd/MM/yyyy") + "\n");
                    break;
                case "cmd.exe":
                    input.Keyboard.TextEntry("dir\n");
                    break;
                case "chrome.exe":
                    input.Keyboard.TextEntry("https://www.google.com/search?q=dotnet+8\n");
                    break;
            }

            await Task.Delay(random.Next(60000, 180000)); // 1-3 minutos
            input.Keyboard.ModifiedKeyStroke(VirtualKeyCode.MENU, VirtualKeyCode.F4); // Alt + F4
        }
    }

    // ===== [5. Cambio de ventanas aleatorio] =====
    static async Task SwitchWindowsRandomly()
    {
        var input = new InputSimulator();
        var random = new Random();

        while (true)
        {
            await RandomDelay(30000, 240000); // 1-4 minutos

            // Alt + Tab
            input.Keyboard.ModifiedKeyStroke(VirtualKeyCode.LMENU, VirtualKeyCode.TAB);
            await Task.Delay(random.Next(500, 1500));

            // 30% de maximizar/restaurar (Win + ↑/↓)
            //if (random.Next(0, 100) < 30)
            //{
            //    input.Keyboard.ModifiedKeyStroke(
            //        VirtualKeyCode.LWIN,
            //        //random.Next(0, 2) == 0 ? VirtualKeyCode.UP : VirtualKeyCode.DOWN
            //        VirtualKeyCode.UP
            //    );
            //}
        }
    }

    // ===== [Helpers] =====
    static async Task RandomDelay(int minMs, int maxMs, double idleProbability = 0.35)
    {
        if (new Random().NextDouble() < idleProbability)
            await Task.Delay(new Random().Next(minMs * 2, maxMs * 2));
        else
            await Task.Delay(new Random().Next(minMs, maxMs));
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    static bool IsCursorInActiveWindow() => GetForegroundWindow() != IntPtr.Zero;
}