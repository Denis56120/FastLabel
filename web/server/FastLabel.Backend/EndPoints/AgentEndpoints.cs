namespace FastLabel.Backend.Endpoints;

public static class AgentEndpoints
{
    public static void MapAgentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/agents").RequireAuthorization();

        group.MapGet("/", async (
            int page, 
            int pageSize, 
            string? sortBy, 
            bool? sortAscending, 
            string? filterByUser,
            string? filterByType,
            AppDbContext db, 
            ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var lang = await db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Language != null ? u.Language.Code : HttpContextExtensions.DefaultLanguage)
                .FirstOrDefaultAsync() ?? HttpContextExtensions.DefaultLanguage;

            var query = db.Agents
                .Include(a => a.AgentType)
                    .ThenInclude(at => at.Name)
                        .ThenInclude(n => n.Translations)
                            .ThenInclude(tr => tr.Language)
                .Include(a => a.User)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(a => a.UserId == userId);
            }

            // Filtres textuels
            if (!string.IsNullOrWhiteSpace(filterByUser))
            {
                query = query.Where(a => a.User != null && a.User.Name.Contains(filterByUser));
            }

            if (!string.IsNullOrWhiteSpace(filterByType))
            {
                query = query.Where(a => a.AgentType != null && a.AgentType.Name != null &&
                    (a.AgentType.Name.Translations
                        .Where(tr => tr.Language.Code == lang)
                        .Select(tr => tr.TranslatedValue)
                        .FirstOrDefault() ?? a.AgentType.Name.DefaultTranslation)
                    .Contains(filterByType));
            }

            var naturalComparer = new NaturalStringComparer();

            var result = await query.ToPagedResultAsync(
                page,
                pageSize,
                sortBy,
                sortAscending,
                a => new AgentDto
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
                    UserName = a.User != null ? a.User.Name : string.Empty,
                    ApiKey = a.ApiKey, 
                    CreatedAt = a.CreatedAt,
                    LastSeenAt = a.LastSeenAt
                },
                (items, sortField, ascending) => sortField?.ToLower() switch
                {
                    "username" => ascending ? items.OrderBy(x => x.UserName, naturalComparer) : items.OrderByDescending(x => x.UserName, naturalComparer),
                    "agenttypename" => ascending ? items.OrderBy(x => x.AgentTypeName, naturalComparer) : items.OrderByDescending(x => x.AgentTypeName, naturalComparer),
                    "createdat" => ascending ? items.OrderBy(x => x.CreatedAt) : items.OrderByDescending(x => x.CreatedAt),
                    "lastseenat" => ascending ? items.OrderBy(x => x.LastSeenAt) : items.OrderByDescending(x => x.LastSeenAt),
                    _ => items.OrderBy(x => x.Id)
                }
            );

            return Results.Ok(result);
        });

        group.MapPost("/", async (AgentDto dto, AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            var typeExists = await db.AgentTypes.AnyAsync(at => at.Id == dto.AgentTypeId);
            if (!typeExists)
            {
                return Results.BadRequest(new { message = "agents.error.invalid_type" });
            }

            // Génération sécurisée côté serveur d'une clé d'API unique
            var secureApiKey = $"fl_{Guid.NewGuid():N}{Guid.NewGuid():N}";

            var agent = new Agent
            {
                AgentTypeId = dto.AgentTypeId,
                UserId = userId,
                ApiKey = secureApiKey, // Stockée en base
                CreatedAt = DateTime.UtcNow
            };

            db.Agents.Add(agent);
            await db.SaveChangesAsync();

            dto.Id = agent.Id;
            dto.CreatedAt = agent.CreatedAt;
            dto.ApiKey = secureApiKey; 

            return Results.Created($"/api/agents/{agent.Id}", dto);
        });

        group.MapPut("/{id:int}", async (int id, AgentDto updatedDto, AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

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

            // On met à jour uniquement les champs autorisés (ex: le type d'agent)
            agent.AgentTypeId = updatedDto.AgentTypeId;
            
            // On s'assure de renvoyer la clé existante (masquée ou non modifiée) dans le DTO de retour
            updatedDto.ApiKey = agent.ApiKey;

            await db.SaveChangesAsync();
            return Results.Ok(updatedDto);
        });

        group.MapDelete("/{id:int}", async (int id, AppDbContext db, PrintBrokerService broker, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

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