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

        group.MapGet("/", async (AppDbContext db, HttpContext httpContext, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin) = GetUserInfo(claimsUser);
            var lang = HttpContextExtensions.GetRequestLanguage(httpContext);

            var query = db.Agents
                .Include(a => a.AgentType)
                    .ThenInclude(at => at.Name)
                        .ThenInclude(n => n.Translations)
                            .ThenInclude(tr => tr.Language)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(a => a.UserId == userId);
            }

            var result = await query.Select(a => new AgentDto
            {
                Id = a.Id,
                AgentTypeId = a.AgentTypeId,
                AgentTypeName = a.AgentType != null && a.AgentType.Name != null
                    ? (a.AgentType.Name.Translations
                        .Where(tr => tr.Language.Code == lang)
                        .Select(tr => tr.TranslatedValue)
                        .FirstOrDefault()
                       ?? a.AgentType.Name.DefaultTranslation)
                    : string.Empty,
                ApiKey = a.ApiKey ?? string.Empty,
                CreatedAt = a.CreatedAt,
                LastSeenAt = a.LastSeenAt
            }).ToListAsync();

            return Results.Ok(result);
        });

        group.MapPost("/", async (AgentDto dto, AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            var (userId, _) = GetUserInfo(claimsUser);

            var typeExists = await db.AgentTypes.AnyAsync(at => at.Id == dto.AgentTypeId);
            if (!typeExists)
            {
                return Results.BadRequest(new { message = "agents.error.invalid_type" });
            }

            var agent = new Agent
            {
                AgentTypeId = dto.AgentTypeId,
                UserId = userId,
                ApiKey = string.IsNullOrWhiteSpace(dto.ApiKey) ? Guid.NewGuid().ToString("N") : dto.ApiKey,
                CreatedAt = DateTime.UtcNow
            };

            db.Agents.Add(agent);
            await db.SaveChangesAsync();

            dto.Id = agent.Id;
            dto.CreatedAt = agent.CreatedAt;
            dto.ApiKey = agent.ApiKey;

            return Results.Created($"/api/agents/{agent.Id}", dto);
        });

        group.MapPut("/{id:int}", async (int id, AgentDto updatedDto, AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin) = GetUserInfo(claimsUser);

            var agent = await db.Agents.FindAsync(id);
            if (agent is null) return Results.NotFound();

            if (!isAdmin && agent.UserId != userId)
            {
                return Results.Forbid();
            }

            var typeExists = await db.AgentTypes.AnyAsync(at => at.Id == updatedDto.AgentTypeId);
            if (!typeExists)
            {
                return Results.BadRequest(new { message = "agents.error.invalid_type" });
            }

            agent.AgentTypeId = updatedDto.AgentTypeId;
            
            updatedDto.ApiKey = agent.ApiKey;

            await db.SaveChangesAsync();
            return Results.Ok(updatedDto);
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
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Agent deleted", CancellationToken.None); 
                } 
                catch {}
            }

            db.Agents.Remove(agent);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}