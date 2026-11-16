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

        group.MapGet("/", async (int page, int pageSize, AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin) = GetUserInfo(claimsUser);
            if (userId == 0) return Results.Unauthorized();

            // Valeurs par défaut de sécurité si non fournies par le client
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            // Récupère la langue de l'utilisateur connecté en base
            var lang = await db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Language != null ? u.Language.Code : HttpContextExtensions.DefaultLanguage)
                .FirstOrDefaultAsync() ?? HttpContextExtensions.DefaultLanguage;

            var query = db.Agents
                .Include(a => a.AgentType)
                    .ThenInclude(at => at.Name)
                        .ThenInclude(n => n.Translations)
                            .ThenInclude(tr => tr.Language)
                .Include(a => a.User) // <--- Ajouté pour récupérer le nom de l'utilisateur
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(a => a.UserId == userId);
            }

            // 1. Calcul du nombre total d'éléments avant pagination
            var totalCount = await query.CountAsync();

            // 2. Application de la pagination (Skip & Take)
            var items = await query
                .OrderBy(a => a.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AgentDto
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
                    UserName = a.User != null ? a.User.Name : string.Empty, // <--- Assignation du UserName
                    ApiKey = a.ApiKey ?? string.Empty,
                    CreatedAt = a.CreatedAt,
                    LastSeenAt = a.LastSeenAt
                })
                .ToListAsync();

            // 3. Encapsulation dans le PagedResultDto
            var result = new PagedResultDto<AgentDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return Results.Ok(result);
        });

        group.MapPost("/", async (AgentDto dto, AppDbContext db, ClaimsPrincipal claimsUser) =>
        {
            var (userId, _) = GetUserInfo(claimsUser);
            if (userId == 0) return Results.Unauthorized();

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

            agent.AgentTypeId = updatedDto.AgentTypeId;
            
            updatedDto.ApiKey = agent.ApiKey;

            await db.SaveChangesAsync();
            return Results.Ok(updatedDto);
        });

        group.MapDelete("/{id:int}", async (int id, AppDbContext db, PrintBrokerService broker, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin) = GetUserInfo(claimsUser);
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