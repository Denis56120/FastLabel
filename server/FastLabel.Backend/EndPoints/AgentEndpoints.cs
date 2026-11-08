using FastLabel.Backend.Data;
using FastLabel.Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Endpoints;

public static class AgentEndpoints
{
    public static void MapAgentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/agents");

        group.MapGet("/", async (AppDbContext db, PrintBrokerService broker) =>
        {
            var agents = await db.Agents.ToListAsync();
            
            var result = agents.Select(a => new
            {
                a.Id,
                a.ApiKey,
                a.LastSeenAt,
                IsConnected = broker.ActiveAgents.ContainsKey(a.Id)
            });

            return Results.Ok(result);
        });

        group.MapDelete("/{id:int}", async (int id, AppDbContext db, PrintBrokerService broker) =>
        {
            var agent = await db.Agents.FindAsync(id);
            if (agent is null) return Results.NotFound();

            if (broker.ActiveAgents.TryRemove(id, out var ws))
            {
                try { await ws.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Agent deleted", CancellationToken.None); } catch {}
            }

            db.Agents.Remove(agent);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}