namespace FastLabel.Backend.Endpoints;

public static class PrintEndpoints
{
    public static void MapPrintEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/print", async (PrintRequest request, AppDbContext db, PrintBrokerService broker) =>
        {
            if (!ValidationExtensions.TryValidateModel(request, out var errorResult))
            {
                return errorResult!;
            }

            var agent = await db.Agents
                .OrderBy(a => a.Id)
                .FirstOrDefaultAsync(a => a.ApiKey == request.ApiKey);
            if (agent == null) return Results.Unauthorized();

            if (!broker.ActiveAgents.TryGetValue(agent.Id, out var ws) || ws.State != WebSocketState.Open)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "print.error.agent_not_connected" });
            }

            var printer = await db.Printers.FindAsync(request.PrinterId);
            if (printer == null)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "printer.error.not_found" });
            }

            string zplData = request.RawData ?? string.Empty;
            if (request.TemplateId.HasValue)
            {
                var template = await db.LabelTemplates.FindAsync(request.TemplateId.Value);
                if (template == null) 
                {
                    return Results.BadRequest(new ErrorResponseDto { Message = "print.error.template_not_found" });
                }
            }

            var job = new PrintJob
            {
                AgentId = agent.Id,
                PrinterId = printer.Id,
                LabelTemplateId = request.TemplateId ?? 0,
                Quantity = request.Quantity,
                InputDataJson = JsonSerializer.Serialize(request.Variables ?? new Dictionary<string, string>()),
                GeneratedPayload = zplData
            };

            db.PrintJobs.Add(job);
            await db.SaveChangesAsync();

            string jobIdStr = job.Id.ToString();
            var tcs = new TaskCompletionSource<(string status, string message)>();
            broker.PendingJobs[jobIdStr] = tcs;

            var payload = JsonSerializer.Serialize(new
            {
                jobId = job.Id,
                ip = printer.IP,
                port = printer.Port,
                data = zplData
            });

            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(10000));
            if (completedTask == tcs.Task)
            {
                var result = await tcs.Task;
                return Results.Ok(new { jobId = job.Id, status = result.status, message = result.message });
            }
            else
            {
                broker.PendingJobs.TryRemove(jobIdStr, out _);
                return Results.BadRequest(new ErrorResponseDto { Message = "print.error.timeout" });
            }
        });
    }
}