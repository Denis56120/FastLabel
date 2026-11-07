using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FastLabel.Backend.Data;
using FastLabel.Backend.Endpoints;
using FastLabel.Backend.Models;
using Microsoft.EntityFrameworkCore;

var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "FastLabel.Server.db");
var builder = WebApplication.CreateBuilder(args);

// Configuration de la BDD SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

var app = builder.Build();

// Initialisation de la BDD et des données de test au démarrage
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    if (!db.Agents.Any())
    {
        db.Agents.Add(new Agent { Name = "Agent de Test Local", ApiKey = "TEST-API-KEY-12345" });
        db.SaveChanges();
    }

    if (!db.Printers.Any())
    {
        db.Printers.Add(new Printer { Name = "Zebra Expéditions", Location = "Quai 1", Ip = "192.168.1.50", Port = 9100 });
        db.SaveChanges();
    }

    if (!db.LabelTemplates.Any())
    {
        db.LabelTemplates.Add(new LabelTemplate { Name = "Étiquette Standard", JsonData = "{}" });
        db.SaveChanges();
    }
}

app.UseWebSockets();

// Dictionnaires globaux pour les WebSockets et les Jobs en attente
var activeAgents = new ConcurrentDictionary<int, WebSocket>();
var pendingJobs = new ConcurrentDictionary<string, TaskCompletionSource<(string status, string message)>>();

// ==========================================
// 1. ENDPOINT WEBSOCKET POUR LES AGENTS
// ==========================================
app.Map("/agent-ws", async (HttpContext context, AppDbContext db) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    string? apiKey = context.Request.Query["apiKey"];
    var agent = await db.Agents.FirstOrDefaultAsync(a => a.ApiKey == apiKey);

    if (agent == null)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    
    agent.LastSeenAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    activeAgents[agent.Id] = webSocket;
    Console.WriteLine($"[WS] Agent '{agent.Name}' (ID: {agent.Id}) connecté.");

    var buffer = new byte[8192];
    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close) break;

            if (result.MessageType == WebSocketMessageType.Text)
            {
                string responseJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                
                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;
                string jobGuid = root.TryGetProperty("jobGuid", out var jProp) ? jProp.GetString() ?? "" : "";
                string status = root.TryGetProperty("status", out var sProp) ? sProp.GetString() ?? "error" : "error";
                string message = root.TryGetProperty("message", out var mProp) ? mProp.GetString() ?? "" : "";

                using var scope = app.Services.CreateScope();
                var dbScoped = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var job = await dbScoped.PrintJobs.FirstOrDefaultAsync(j => j.JobGuid == jobGuid);
                
                if (job != null)
                {
                    job.Status = status;
                    job.ResponseMessage = message;
                    job.ProcessedAt = DateTime.UtcNow;
                    await dbScoped.SaveChangesAsync();
                }

                if (pendingJobs.TryRemove(jobGuid, out var tcs))
                {
                    tcs.TrySetResult((status, message));
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[WS Erreur] {agent.Name} : {ex.Message}");
    }
    finally
    {
        activeAgents.TryRemove(agent.Id, out _);
        Console.WriteLine($"[WS] Agent '{agent.Name}' déconnecté.");
    }
});

// ==========================================
// 2. ENDPOINT PRINCIPAL D'IMPRESSION (/api/print)
// ==========================================
app.MapPost("/api/print", async (PrintRequest request, AppDbContext db) =>
{
    var agent = await db.Agents.FirstOrDefaultAsync(a => a.ApiKey == request.ApiKey);
    if (agent == null) return Results.Unauthorized();

    if (!activeAgents.TryGetValue(agent.Id, out var ws) || ws.State != WebSocketState.Open)
    {
        return Results.Problem(detail: $"L'agent '{agent.Name}' n'est pas connecté.", statusCode: 503);
    }

    var printer = await db.Printers.FindAsync(request.PrinterId);
    if (printer == null || !printer.IsActive)
    {
        return Results.BadRequest(new { message = "Imprimante introuvable ou inactive." });
    }

    string zplData = request.RawData ?? string.Empty;
    if (request.TemplateId.HasValue)
    {
        var template = await db.LabelTemplates.FindAsync(request.TemplateId.Value);
        if (template == null) return Results.BadRequest(new { message = "Modèle introuvable." });
        // (Le moteur de fusion ZPL / Variables viendra s'insérer ici)
    }

    var job = new PrintJob
    {
        AgentId = agent.Id,
        PrinterId = printer.Id,
        TargetIp = printer.Ip,
        TargetPort = printer.Port,
        TemplateId = request.TemplateId,
        InputDataJson = JsonSerializer.Serialize(request.Variables ?? new Dictionary<string, string>()),
        GeneratedPayload = zplData,
        Status = "Pending"
    };

    db.PrintJobs.Add(job);
    await db.SaveChangesAsync();

    var tcs = new TaskCompletionSource<(string status, string message)>();
    pendingJobs[job.JobGuid] = tcs;

    var payload = JsonSerializer.Serialize(new
    {
        jobGuid = job.JobGuid,
        ip = printer.Ip,
        port = printer.Port,
        data = zplData
    });

    byte[] bytes = Encoding.UTF8.GetBytes(payload);
    await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);

    var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(10000));
    if (completedTask == tcs.Task)
    {
        var result = await tcs.Task;
        return Results.Ok(new { jobGuid = job.JobGuid, status = result.status, message = result.message });
    }
    else
    {
        pendingJobs.TryRemove(job.JobGuid, out _);
        return Results.Problem(detail: "Timeout : L'agent local n'a pas répondu.", statusCode: 504);
    }
});

// ==========================================
// 3. ENREGISTREMENT DES MODULES DE ROUTES
// ==========================================
app.MapPrinterEndpoints();
app.MapTemplateEndpoints();

// Lancement de l'application
app.Run("http://localhost:5000");

// ==========================================
// DTOs (Records)
// ==========================================
public record PrintRequest(
    string ApiKey,
    int PrinterId,
    int? TemplateId,
    Dictionary<string, string>? Variables,
    string? RawData
);