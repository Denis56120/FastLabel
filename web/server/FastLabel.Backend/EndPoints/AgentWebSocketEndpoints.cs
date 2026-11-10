using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FastLabel.Backend.Data;
using FastLabel.Backend.Models;
using FastLabel.Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Endpoints;

public static class AgentWebSocketEndpoints
{
    public static void MapAgentWebSocketEndpoints(this IEndpointRouteBuilder app)
    {
        app.Map("/agent-ws", async (HttpContext context, PrintBrokerService broker) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            string? apiKey = context.Request.Query["apiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            Agent? agent;
            using (var scope = context.RequestServices.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                agent = await db.Agents.FirstOrDefaultAsync(a => a.ApiKey == apiKey);
                
                if (agent != null)
                {
                    agent.LastSeenAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }

            if (agent == null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            
            broker.ActiveAgents[agent.Id] = webSocket;

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
                        
                        int jobId = root.TryGetProperty("jobId", out var jProp) && jProp.TryGetInt32(out var parsedId) ? parsedId : 0;
                        string status = root.TryGetProperty("status", out var sProp) ? sProp.GetString() ?? "error" : "error";
                        string message = root.TryGetProperty("message", out var mProp) ? mProp.GetString() ?? "" : "";

                        using var scope = context.RequestServices.CreateScope();
                        var dbScoped = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        
                        var job = await dbScoped.PrintJobs.FirstOrDefaultAsync(j => j.Id == jobId);
                        
                        if (job != null)
                        {
                            job.ResponseMessage = message;
                            job.ProcessedAt = DateTime.UtcNow;
                            await dbScoped.SaveChangesAsync();
                        }

                        string jobIdStr = jobId.ToString();
                        if (broker.PendingJobs.TryRemove(jobIdStr, out var tcs))
                        {
                            tcs.TrySetResult((status, message));
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                broker.ActiveAgents.TryRemove(agent.Id, out _);
            }
        });
    }
}