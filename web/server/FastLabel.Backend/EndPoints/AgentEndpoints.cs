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
            ClaimsPrincipal claimsUser,
            HttpContext httpContext) =>
        {
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 500) pageSize = 500;

            var (languageId, language) = await DbHelpers.GetUserLanguageByUserIdAsync(httpContext, db, userId);

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

            if (!string.IsNullOrWhiteSpace(filterByUser))
            {
                query = query.Where(a => a.User != null && a.User.Name.Contains(filterByUser));
            }

            if (!string.IsNullOrWhiteSpace(filterByType))
            {
                query = query.Where(a => a.AgentType != null && a.AgentType.Name != null &&
                    (a.AgentType.Name.Translations
                        .Where(tr => tr.Language.Code == language)
                        .Select(tr => tr.TranslatedValue)
                        .FirstOrDefault() ?? a.AgentType.Name.DefaultTranslation)
                    .Contains(filterByType));
            }

            var naturalComparer = new NaturalStringHelpers();

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
                            .Where(tr => tr.Language.Code == language)
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
            if (!ValidationHelpers.TryValidateModel(dto, out var errorResult))
            {
                return errorResult!;
            }

            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            var agenttype = await DbHelpers.GetAgentTypeByIdAsync(db, dto.AgentTypeId);
            if (agenttype == null)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "agents.error.invalid_type" });
            }

            var secureApiKey = $"fl_{Guid.NewGuid():N}{Guid.NewGuid():N}";
            var agent = new Agent
            {
                AgentTypeId = dto.AgentTypeId,
                UserId = userId,
                ApiKey = secureApiKey,
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
            if (!ValidationHelpers.TryValidateModel(updatedDto, out var errorResult))
            {
                return errorResult!;
            }

            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            var agent = await DbHelpers.GetAgentByIdAsync(db, id);
            if (agent is null) return Results.NotFound(new ErrorResponseDto { Message = "agent.error.not_found" });

            if (!isAdmin && agent.UserId != userId)
            {
                return Results.Forbid();
            }

            var agenttype = await DbHelpers.GetAgentTypeByIdAsync(db, updatedDto.AgentTypeId);
            if (agenttype == null)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "agents.error.invalid_type" });
            }

            agent.AgentTypeId = updatedDto.AgentTypeId;

            await db.SaveChangesAsync();

            updatedDto.Id = agent.Id;
            updatedDto.ApiKey = agent.ApiKey;
            updatedDto.CreatedAt = agent.CreatedAt;
            updatedDto.LastSeenAt = agent.LastSeenAt;
            return Results.Ok(updatedDto);
        });

        group.MapDelete("/{id:int}", async (int id, AppDbContext db, PrintBrokerService broker, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            var agent = await DbHelpers.GetAgentByIdAsync(db, id);
            if (agent is null) return Results.NotFound(new ErrorResponseDto { Message = "agent.error.not_found" });

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
                catch (Exception ex)
                {
                    AppLog.Error(ex);
                }
            }

            db.Agents.Remove(agent);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapPost("/delete-batch", async (List<int>? ids, AppDbContext db, PrintBrokerService broker, ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            if (ids == null || ids.Count == 0)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "common.error.no_ids_selected" });
            }

            var distinctIds = ids.Distinct().ToList();

            var query = db.Agents.Where(a => distinctIds.Contains(a.Id));
            if (!isAdmin)
            {
                query = query.Where(a => a.UserId == userId);
            }

            var agents = await query.ToListAsync();
            if (agents.Count == 0)
            {
                return Results.NotFound(new ErrorResponseDto { Message = "agent.error.not_found" });
            }

            foreach (var agent in agents)
            {
                if (broker.ActiveAgents.TryRemove(agent.Id, out var ws))
                {
                    try
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Agent deleted", CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        AppLog.Error(ex);
                    }
                }
            }

            db.Agents.RemoveRange(agents);
            await db.SaveChangesAsync();

            return Results.Ok(new { deletedCount = agents.Count });
        });
    }
}