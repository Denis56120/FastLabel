namespace FastLabel.Backend.Endpoints;

public static class ClientEndpoints
{
    public static void MapClientEndpoints(this IEndpointRouteBuilder routes)
    {
        var clientGroup = routes.MapGroup("/api/clients").RequireAuthorization();

        clientGroup.MapGet("/", async (
            int? page, 
            int? pageSize, 
            string? sortBy, 
            bool? sortAscending, 
            string? filterByName,
            string? filterByEmail,
            AppDbContext db, 
            ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            var user = await DbHelpers.GetUserByIdAsync(db, userId);  
            if (user == null || !user.IsReseller)
                return Results.Forbid();

            int currentPage = page ?? 1;
            int size = pageSize ?? 10;

            var query = db.Users.Where(u => u.ResellerId == userId).AsQueryable();

            if (!string.IsNullOrWhiteSpace(filterByName))
            {
                query = query.Where(u => u.Name != null && u.Name.Contains(filterByName));
            }

            if (!string.IsNullOrWhiteSpace(filterByEmail))
            {
                query = query.Where(u => u.Email != null && u.Email.Contains(filterByEmail));
            }

            var naturalComparer = new NaturalStringHelpers();

            var result = await query.ToPagedResultAsync(
                currentPage,
                size,
                sortBy,
                sortAscending,
                u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    IsAdmin = u.IsAdmin,
                    IsReseller = u.IsReseller,
                    ResellerId = u.ResellerId,
                    LanguageId = u.LanguageId,
                    CreatedAt = u.CreatedAt
                },
                (items, sortField, ascending) => sortField?.ToLower() switch
                {
                    "name" => ascending ? items.OrderBy(x => x.Name, naturalComparer) : items.OrderByDescending(x => x.Name, naturalComparer),
                    "email" => ascending ? items.OrderBy(x => x.Email, naturalComparer) : items.OrderByDescending(x => x.Email, naturalComparer),
                    "createdat" => ascending ? items.OrderBy(x => x.CreatedAt) : items.OrderByDescending(x => x.CreatedAt),
                    _ => items.OrderBy(x => x.Id)
                }
            );

            return Results.Ok(result);
        });

        clientGroup.MapPost("/", async (
            UserDto clientDto, 
            AppDbContext db, 
            IEmailService emailService, 
            IConfiguration configuration, 
            IStripeService stripeService, 
            HttpContext httpContext, 
            ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            var reseller = await DbHelpers.GetUserByIdWithStripeRecordAsync(db, userId); 
            if (reseller == null || !reseller.IsReseller)
                return Results.Forbid();

            if (!ValidationHelpers.TryValidateModel(clientDto, out var errorResult))
            {
                return errorResult!;
            }

            var user = await DbHelpers.GetUserByEmailAsync(db, clientDto.Email); 
            if (user != null)
            {
                return Results.BadRequest(new ErrorResponseDto { Message = "user.error.email_already_taken" });
            }

            if (reseller.StripeRecord != null && !string.IsNullOrEmpty(reseller.StripeRecord.StripeSubscriptionId))
            {
                try
                {
                    await stripeService.IncrementSubscriptionQuantityAsync(reseller.StripeRecord.StripeSubscriptionId);
                }
                catch (Exception ex)
                {
                    AppLog.Error(ex);
                    return Results.BadRequest(new ErrorResponseDto { Message = "reseller.error.stripe_update_failed" });
                }
            }

            int inactiveStatusId = await DbHelpers.GetStatusIdByTagAsync(db, "status.inactive");

            var activationToken = Guid.NewGuid().ToString();

            var (languageId, language) = await DbHelpers.GetUserLanguageByUserIdAsync(httpContext, db, reseller.LanguageId);

            var newClient = new User
            {
                Name = clientDto.Name,
                Email = clientDto.Email,
                LanguageId = languageId,
                BasicStatusId = inactiveStatusId,
                ResellerId = userId,
                IsReseller = false,
                EmailVerificationToken = activationToken,
                CreatedAt = DateTime.UtcNow
            };

            await DbHelpers.CreateUserAndSendActivationEmailAsync(db, emailService, configuration, newClient, language);

            clientDto.Id = newClient.Id;
            clientDto.Password = null;

            return Results.Created($"/api/clients/{newClient.Id}", clientDto);
        });

        clientGroup.MapDelete("/{id:int}", async (
            int id, 
            AppDbContext db, 
            IStripeService stripeService, 
            ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            var reseller = await DbHelpers.GetUserByIdWithStripeRecordAsync(db, userId);  

            if (reseller == null || !reseller.IsReseller)
                return Results.Forbid();

            var clientToDelete = await DbHelpers.GetUserByIdAndResellerIdAsync(db, id, userId);  
            if (clientToDelete == null)
                return Results.NotFound(new ErrorResponseDto { Message = "user.error.not_found" });

            if (reseller.StripeRecord != null && !string.IsNullOrEmpty(reseller.StripeRecord.StripeSubscriptionId))
            {
                try
                {
                    await stripeService.DecrementSubscriptionQuantityAsync(reseller.StripeRecord.StripeSubscriptionId);
                }
                catch (Exception ex)
                {
                    AppLog.Error(ex);
                    return Results.BadRequest(new ErrorResponseDto { Message = "reseller.error.stripe_update_failed" });
                }
            }

            db.Users.Remove(clientToDelete);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        clientGroup.MapPost("/delete-batch", async (
            int[] ids,
            AppDbContext db,
            IStripeService stripeService,
            ClaimsPrincipal claimsUser) =>
        {
            var (userId, isAdmin, isReseller) = claimsUser.GetUserInfo();
            if (userId == 0) return Results.Unauthorized();

            var reseller = await DbHelpers.GetUserByIdWithStripeRecordAsync(db, userId);

            if (reseller == null || !reseller.IsReseller)
                return Results.Forbid();

            if (ids == null || ids.Length == 0)
                return Results.BadRequest(new ErrorResponseDto { Message = "common.error.empty_selection" });

            var distinctIds = ids.Distinct().ToList();

            var clientsToDelete = await db.Users
                .Where(u => u.ResellerId == userId && distinctIds.Contains(u.Id))
                .ToListAsync();

            if (clientsToDelete.Count == 0)
                return Results.NotFound(new ErrorResponseDto { Message = "user.error.not_found" });

            if (reseller.StripeRecord != null && !string.IsNullOrEmpty(reseller.StripeRecord.StripeSubscriptionId))
            {
                try
                {
                    for (int i = 0; i < clientsToDelete.Count; i++)
                    {
                        await stripeService.DecrementSubscriptionQuantityAsync(reseller.StripeRecord.StripeSubscriptionId);
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Error(ex);
                    return Results.BadRequest(new ErrorResponseDto { Message = "reseller.error.stripe_update_failed" });
                }
            }

            db.Users.RemoveRange(clientsToDelete);
            await db.SaveChangesAsync();

            return Results.Ok(new { DeletedCount = clientsToDelete.Count });
        });
    }
}