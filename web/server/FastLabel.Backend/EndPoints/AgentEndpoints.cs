using FastLabel.Backend.Data;
using FastLabel.Backend.Models;
using FastLabel.Backend.Services;
using FastLabel.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FastLabel.Backend.Endpoints;

public static class AgentEndpoints
{
    public static void MapAgentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/agents").RequireAuthorization();

        static (int userId, bool isAdmin) GetUserInfo(ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                           ?? user.FindFirst("nameid")?.Value 
                           ?? user.FindFirst("unique_name")?.Value;

            int.TryParse(userIdClaim, out var userId);
            bool isAdmin = user.IsInRole("Admin");

            return (userId, isAdmin);
        }

        group.MapGet("/", async (AppDbContext db, PrintBrokerService broker, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin) = GetUserInfo(claimsUser);

            var query = db.Agents
                .Include(a => a.AgentType)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(a => a.UserId == userId);
            }

            var agents = await query.ToListAsync();

            var result = agents.Select(a => new AgentDto
            {
                Id = a.Id,
                Name = (a.AgentType != null && a.AgentType.Name != null ? a.AgentType.Name.ToString() : null) ?? "Agent",
                AgentTypeName = (a.AgentType != null && a.AgentType.Name != null ? a.AgentType.Name.ToString() : null) ?? string.Empty,
                ApiKey = a.ApiKey ?? string.Empty,
                CreatedAt = a.CreatedAt,
                LastSeenAt = a.LastSeenAt
            }).ToList();

            return Results.Ok(result);
        });

        group.MapDelete("/{id:int}", async (int id, AppDbContext db, PrintBrokerService broker, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin) = GetUserInfo(claimsUser);

            var agent = await db.Agents.FindAsync(id);
            if (agent is null) return Results.NotFound();

            if (!isAdmin && agent.UserId != userId)
            {
                return Results.Forbid();
            }

            if (broker.ActiveAgents.TryRemove(id, out var ws))
            {
                try 
                { 
                    await ws.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Agent deleted", CancellationToken.None); 
                } 
                catch {}
            }

            db.Agents.Remove(agent);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}